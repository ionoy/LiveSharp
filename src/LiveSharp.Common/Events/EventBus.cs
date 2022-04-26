using LiveSharp.Common.Events;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace LiveSharp.Common.Events
{
    public class EventBus : IEventBus
    {
        public IObservable<Event> Events => _replaySubject.AsObservable();

        private ReplaySubject<Event> _replaySubject = new ReplaySubject<Event>();

        public void PublishEvent(Event evt)
        {
            _replaySubject.OnNext(evt);
        }
    }
}
