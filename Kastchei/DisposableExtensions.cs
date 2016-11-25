using System;
using System.Reactive.Disposables;

namespace Kastchei
{
    public static class DisposableExtensions
    {
        public static TDisposable DisposeWith<TDisposable>(this TDisposable observable, CompositeDisposable disposables)
            where TDisposable : class, IDisposable
        {
            if (observable != null)
                disposables.Add(observable);

            return observable;
        }

        public static TDisposable DisposeWith<TDisposable>(this TDisposable observable, Lazy<CompositeDisposable> disposables)
            where TDisposable : class, IDisposable
        {
            if (observable != null)
                disposables.Value.Add(observable);

            return observable;
        }
    }
}
