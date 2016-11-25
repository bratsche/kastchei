using System;
using System.Reactive.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kastchei
{
    public static class ObservableExtensions
    {
        public static IObservable<T> MatchOn<T>(this IObservable<JObject> observable, string match)
        {
            return observable.Where(x => x["payload"]["status"].Value<string>() == match)
                             .Select(x => JsonConvert.DeserializeObject<Frame<T>>(x.ToString()))
                             .Select(x => x.Payload.Response);
        }
    }
}
