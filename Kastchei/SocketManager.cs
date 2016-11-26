using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Disposables;
using System.Collections.Generic;

using WebSocket4Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kastchei
{
    public class SocketManager : IDisposable
    {
        const int HEARTBEAT_INTERVAL = 30000;

        public SocketManager(string endpoint)
        {
            /* Setup the socket */
            socket = new WebSocket(endpoint).DisposeWith(compositeDisposable);
            Frames = Observable.FromEventPattern<MessageReceivedEventArgs>(socket, "MessageReceived")
                               .Select(x => JObject.Parse(x.EventArgs.Message));

            /* This determines when we want the socket to be open */
            needOpenSubject = new BehaviorSubject<bool>(false).DisposeWith(compositeDisposable);

            /* WhenOpen lets us know when the socket actually is open. */
            var connectingSubject = new BehaviorSubject<SocketState>(SocketState.None);
            var WhenOpen = Observable.Create<SocketState>(observer => {
                var connect = new EventHandler((o, e) => observer.OnNext(SocketState.Open));
                var error = new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>((o, e) => {
                    if (socket.State == WebSocketState.Closed) {
                        observer.OnNext(SocketState.Closed);
                    } else {
                        socket.Close();
                        observer.OnNext(SocketState.Closing);
                    }
                });

                var closed = new EventHandler((o, e) => {
                    if (socket.State == WebSocketState.Closed)
                        observer.OnNext(SocketState.Closed);
                });

                socket.Opened += connect;
                socket.Closed += closed;
                socket.Error += error;

                return () => {
                    socket.Opened -= connect;
                    socket.Closed -= closed;
                    socket.Error -= error;
                };
            }).Merge(connectingSubject.AsObservable());

            /* Heartbeat */
            WhenOpen.CombineLatest(needOpenSubject.AsObservable().DistinctUntilChanged(),
                                   Observable.Interval(TimeSpan.FromMilliseconds(HEARTBEAT_INTERVAL)),
                                   (currentState, needOpen, _time) => currentState == SocketState.Open && needOpen)
                    .Where(x => x == true)
                    .Subscribe(x => SendHeartbeat());

            /* State changes */
            var stateChanges = WhenOpen.DistinctUntilChanged()
                                       .Scan(new StateChange(), (prev, next) => new StateChange(prev.Current, next));

            /* Control whether to open or close the socket. */
            stateChanges.Select(x => x.Current)
                        .CombineLatest(needOpenSubject.DistinctUntilChanged(), (isOpen, needOpen) => {
                return new Tuple<SocketState, bool>(isOpen, needOpen);
            }).Delay(TimeSpan.FromMilliseconds(250)).Subscribe(x => {
                var isOpen = x.Item1;
                var needOpen = x.Item2;

                if (!(isOpen == SocketState.Open || isOpen == SocketState.Opening) && needOpen) {
                    if (socket.State != WebSocketState.Connecting) {
                        connectingSubject.OnNext(SocketState.Opening);
                        socket.Open();
                    }
                } else if (isOpen == SocketState.Open && !needOpen) {
                    socket.Close();
                    connectingSubject.OnNext(SocketState.Closing);
                }
            }).DisposeWith(compositeDisposable);

            /* Setup logic for sending messages. We queue them up until WhenOpen informs us that we're connected, then send */
            var published = sendSubject.Publish();
            published.Subscribe(x => socket.Send(x)).DisposeWith(compositeDisposable);

            WhenOpen.Subscribe(x => {
                if (x == SocketState.Open) {
                    sendConnection = new CompositeDisposable(published.Connect(), Disposable.Create(() => sendSubject.Clear()));
                } else if (sendConnection != null) {
                    sendConnection.Dispose();
                }
            }).DisposeWith(compositeDisposable);
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue) {
                if (disposing) {
                    if (socket.State == WebSocketState.Open)
                        needOpenSubject.OnNext(false);

                    compositeDisposable.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        internal IObservable<JObject> Frames { get; set; }
        WebSocket socket;
        UInt64 currentRef = 0;
        int n_channels = 0;
        bool disposedValue = false;
        IDisposable sendConnection;
        CompositeDisposable compositeDisposable = new CompositeDisposable();
        SocketSendSubject sendSubject = new SocketSendSubject();
        BehaviorSubject<bool> needOpenSubject;
    }
}
