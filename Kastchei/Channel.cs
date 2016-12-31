using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kastchei
{
    public class Channel : IDisposable
    {
        public string Topic { get { return topic; } }

        internal Channel(string topic, SocketManager manager)
        {
            this.topic = topic;
            this.manager = manager;
            this.observable = manager.Frames.Where(x => x["topic"].Value<string>() == topic);
        }

        public IObservable<JObject> Join()
        {
            return Send("phx_join", null);
        }

        public IObservable<JObject> Leave()
        {
            return Send("phx_leave", null);
        }

        public IObservable<JObject> On(string evt)
        {
            return observable.Where(x => x["event"].Value<string>() == evt && x["ref"].Value<string>() == null);
        }

        public IObservable<T> On<T>(string evt)
        {
            return observable.Where(x => x["event"].Value<string>() == evt && x["ref"].Value<string>() == null)
                             .Select(x => JsonConvert.DeserializeObject<Frame<T>>(x.ToString()).Payload.Response);
        }

        public IObservable<JObject> Send(string evt, Dictionary<string, string> payload)
        {
            var frameRef = manager.MakeRef();
            var frame = new PushFrame {
                Topic = topic,
                Event = evt,
                Payload = payload,
                Ref = frameRef
            };
            var json = JsonConvert.SerializeObject(frame);

            var ret = observable.FirstAsync(x => x["event"].Value<string>() == "phx_reply" && x["ref"].Value<UInt64>() == frameRef);

            manager.Send(json);

            return ret;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed) {
                if (disposing) {
                    manager.RemoveChannel();
                    manager = null;
                    observable = null;
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        string topic;
        private bool disposed = false;
        SocketManager manager;
        IObservable<JObject> observable;
    }
}
