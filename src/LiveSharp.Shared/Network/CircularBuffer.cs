using System;
using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    internal class CircularBuffer<T>
    {
        private readonly IReadOnlyList<T> _elements;
        private int _counter;

        public CircularBuffer(IReadOnlyList<T> elements)
        {
            if (elements.Count == 0)
                throw new InvalidOperationException("Can't initialize circular buffer with an empty collection");

            _elements = elements;
        }

        public T Get()
        {
            return _elements[_counter++ % _elements.Count];
        }
    }
}