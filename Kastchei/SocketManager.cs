using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using WebSocket4Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kastchei
{
    public class SocketManager : IDisposable, INotifyPropertyChanged
    {
        const int HEARTBEAT_INTERVAL = 30000;
        const string FORBIDDEN_MESSAGE = "HTTP/1.1 403 Forbidden";

        public event PropertyChangedEventHandler PropertyChanged;

        public IObservable<SocketState> State
        {
            get { return publicState.AsObservable(); }
        }

        WebSocket Socket
        {
            get { return socket; }
            set
            {
                this.socket = value;
                OnPropertyChanged();
            }
        }

        internal IObservable<JObject> Frames
        {
            get { return framesSubject.AsObservable(); }
        }

        public SocketManager(string endpoint)
            : this(Observable.Return(endpoint))
        {
        }

        public SocketManager(IObservable<string> endpointObservable)
        {
            /* This determines when we want the socket to be open */
            needOpenSubject = new BehaviorSubject<bool>(false);

            /* Setup the frame management */
            this.GetPropertyValues(x => x.Socket)
                .Subscribe(x =>
                {
                    if (x == null)
                    {
                        if (frameDisposable != null)
                        {
                            frameDisposable.Dispose();
                            frameDisposable = null;
                        }
                    }
                    else
                    {
                        frameDisposable =
                            Observable.FromEventPattern<MessageReceivedEventArgs>(x, "MessageReceived")
                                      .Select(y => JObject.Parse(y.EventArgs.Message))
                                      .Subscribe(y => framesSubject.OnNext(y))
                                      .DisposeWith(compositeDisposable);
                    }
                }).DisposeWith(compositeDisposable);

            this.GetPropertyValues(x => x.Socket)
                .DistinctUntilChanged()
                .Where(x => x == null)
                .Zip(endpointObservable, (x, y) => y)
                .Subscribe(x =>
                {
                    connectingSubject = new BehaviorSubject<SocketState>(SocketState.None);
                    Socket = new WebSocket(x).DisposeWith(compositeDisposable);
                }).DisposeWith(compositeDisposable);

            this.GetPropertyValues(x => x.Socket)
                .DistinctUntilChanged()
                .Where(x => x != null)
                .Subscribe(x =>
                {
                    var whenOpen = GetConnectionStateObservable(x);

                    /* Heartbeat */
                    whenOpen.CombineLatest(needOpenSubject.AsObservable().DistinctUntilChanged(),
                                           Observable.Interval(TimeSpan.FromMilliseconds(HEARTBEAT_INTERVAL)),
                                           (currentState, needOpen, _time) => currentState == SocketState.Open && needOpen)
                            .Catch<bool, Exception>(ex => Observable.Return(false))
                            .Where(y => y)
                            .Subscribe(_ => SendHeartbeat());

                    /* Control whether to open or close the socket */
                    whenOpen.DistinctUntilChanged()
                            .Scan(new StateChange(), (prev, next) => new StateChange(prev.Current, next))
                            .Select(s => s.Current)
                            .CombineLatest(needOpenSubject.DistinctUntilChanged(), (isOpen, needOpen) => new Tuple<SocketState, bool>(isOpen, needOpen))
                            .Delay(TimeSpan.FromMilliseconds(250))
                            .Catch<Tuple<SocketState, bool>, Exception>(ex =>
                            {
                                x.Close();
                                connectingSubject.Dispose();
                                connectingSubject = null;
                                this.Socket = null;
                                publicState.OnNext(SocketState.Errored);
                                return Observable.Return(new Tuple<SocketState, bool>(SocketState.None, false));
                            })
                            .Subscribe(t =>
                            {
                                var isOpen = t.Item1;
                                var needOpen = t.Item2;

                                if (!(isOpen == SocketState.Open || isOpen == SocketState.Opening) && needOpen)
                                {
                                    if (x.State != WebSocketState.Connecting)
                                    {
                                        connectingSubject.OnNext(SocketState.Opening);
                                        x.Open();
                                    }
                                }
                                else if (isOpen == SocketState.Open && !needOpen)
                                {
                                    x.Close();
                                    connectingSubject.OnNext(SocketState.Closing);
                                }
                                publicState.OnNext(isOpen);
                            }).DisposeWith(compositeDisposable);

                    /* Setup logic for sending messages. We queue them up until we're connected, then send. */
                    var published = sendSubject.Publish();
                    published.Subscribe(p => x.Send(p)).DisposeWith(compositeDisposable);

                    whenOpen.Catch<SocketState, Exception>(ex => Observable.Return(SocketState.None))
                            .Subscribe(s =>
                            {
                                if (s == SocketState.Open)
                                    sendConnection = new CompositeDisposable(published.Connect(), Disposable.Create(() => sendSubject.Clear()));
                                else if (sendConnection != null)
                                    sendConnection.Dispose();
                            }).DisposeWith(compositeDisposable);
                });
        }

        IObservable<SocketState> GetConnectionStateObservable(WebSocket sock)
        {
            return Observable.Create<SocketState>(observer =>
            {
                var connect = new EventHandler((o, e) =>
                {
                    observer.OnNext(SocketState.Open);
                });
                var error = new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((o, e) =>
                {
                    if (e.Exception.Message == FORBIDDEN_MESSAGE)
                    {
                        observer.OnError(e.Exception);
                    }

                    if (sock.State == WebSocketState.Closed)
                    {
                        observer.OnNext(SocketState.Closed);
                    }
                    else
                    {
                        sock.Close();
                        observer.OnNext(SocketState.Closing);
                    }
                });

                var closed = new EventHandler((o, e) =>
                {
                    if (sock.State == WebSocketState.Closed)
                        observer.OnNext(SocketState.Closed);
                });

                sock.Opened += connect;
                sock.Closed += closed;
                sock.Error += error;

                return () =>
                {
                    sock.Opened -= connect;
                    sock.Closed -= closed;
                    sock.Error -= error;
                };
            }).Merge(connectingSubject.AsObservable());
        }

        public Channel Channel(string topic)
        {
            var ret = new Channel(topic, this);
            needOpenSubject.OnNext(true);
            n_channels++;

            return ret;
        }

        internal void RemoveChannel()
        {
            if (--n_channels == 0)
                needOpenSubject.OnNext(false);
        }

        internal UInt64 MakeRef()
        {
            return currentRef++;
        }

        internal void Send(string json)
        {
            sendSubject.OnNext(json);
        }

        void SendHeartbeat()
        {
            var frame = new PushFrame {
                Topic = "phoenix",
                Event = "heartbeat",
                Payload = new Dictionary<string, string>(),
                Ref = MakeRef()
            };
            var json = JsonConvert.SerializeObject(frame);

            Send(json);
        }

        protected void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(prop));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) {
                if (disposing) {
                    if (Socket.State == WebSocketState.Open)
                        needOpenSubject.OnNext(false);

                    compositeDisposable.Dispose();
                    needOpenSubject.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        Subject<JObject> framesSubject = new Subject<JObject>();
        BehaviorSubject<SocketState> connectingSubject;
        Subject<SocketState> publicState = new Subject<SocketState>();
        WebSocket socket;
        UInt64 currentRef = 0;
        int n_channels = 0;
        bool disposedValue = false;
        IDisposable sendConnection;
        IDisposable frameDisposable;
        CompositeDisposable compositeDisposable = new CompositeDisposable();
        SocketSendSubject sendSubject = new SocketSendSubject();
        BehaviorSubject<bool> needOpenSubject;
    }
}
