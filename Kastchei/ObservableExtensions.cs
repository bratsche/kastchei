using System;
using System.Reactive.Linq;
using System.ComponentModel;
using System.Linq.Expressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kastchei
{
    public static class ObservableExtensions
    {
        public static IObservable<T> MatchOn<T>(this IObservable<JObject> observable, string match)
        {
            return observable.Where(x => x["payload"]["status"].Value<string>() == match)
                             .Select(x => JsonConvert.DeserializeObject<ResponseFrame<T>>(x.ToString()))
                             .Select(x => x.Payload.Response);
        }

        public static IObservable<TProperty> GetPropertyValues<TSource, TProperty>(this TSource source,
            Expression<Func<TSource, TProperty>> propertyAccessor) where TSource : INotifyPropertyChanged
        {
            var expression = (MemberExpression)propertyAccessor.Body;

            Func<TSource, TProperty> accesor = propertyAccessor.Compile();

            return Observable.Defer(
                () => Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    x => new PropertyChangedEventHandler(x),
                    x => source.PropertyChanged += x,
                    x => source.PropertyChanged -= x
                )
                .Where(e => e.EventArgs.PropertyName == expression.Member.Name)
                .Select(x => accesor(source))
                .StartWith(accesor(source))
            );
        }
    }
}
