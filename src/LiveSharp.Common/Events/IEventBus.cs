using LiveSharp.Common.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiveSharp.Common.Events
{
    public interface IEventBus
    {
        IObservable<Event> Events { get; }
        void PublishEvent(Event evt);
    }
}
