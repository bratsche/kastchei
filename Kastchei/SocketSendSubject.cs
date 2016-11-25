using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Kastchei
{
    class SocketSendSubject : ISubject<string>
    {
        public SocketSendSubject()
        {
            subject = new ReplaySubject<IObservable<string>>();
            concated = subject.Concat();
            currentSubject = new ReplaySubject<string>();

            subject.OnNext(currentSubject);
        }

        public void Clear()
        {
            currentSubject.OnCompleted();
            currentSubject = new ReplaySubject<string>();
            subject.OnNext(currentSubject);
        }

        public void OnNext(string value)
        {
            currentSubject.OnNext(value);
        }

        public void OnError(Exception ex)
        {
            currentSubject.OnError(ex);
        }

        public void OnCompleted()
        {
            currentSubject.OnCompleted();
            subject.OnCompleted();
            currentSubject = new Subject<string>();
        }

        public IDisposable Subscribe(IObserver<string> observer)
        {
            return concated.Subscribe(observer);
        }

        readonly ReplaySubject<IObservable<string>> subject;
        readonly IObservable<string> concated;
        ISubject<string> currentSubject;
    }
}
