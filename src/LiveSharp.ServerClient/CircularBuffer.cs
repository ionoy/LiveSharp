using System;
using System.Collections.Generic;

namespace LiveSharp.Runtime.Infrastructure
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