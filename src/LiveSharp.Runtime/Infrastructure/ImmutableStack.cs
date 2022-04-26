using System;
using System.Collections;
using System.Collections.Generic;

namespace LiveSharp.Runtime.Infrastructure
{
    public class ImmutableStack<T> : IEnumerable<T>
    {
        private readonly ImmutableStack<T> _tail;
        private readonly T _head;
        private readonly object _metadata;

        public bool IsEmpty { get; }
        public int Count { get; }

        public ImmutableStack(ImmutableStack<T> tail, T head, object metadata = null)
        {
            _tail = tail;
            _head = head;
            _metadata = metadata;

            Count = tail.Count + 1;
        }

        public ImmutableStack()
        {
            IsEmpty = true;
        }

        public ImmutableStack<T> Push(T element, object metadata = null)
        {
            return new ImmutableStack<T>(this, element, metadata);
        }

        public (ImmutableStack<T>, T, object) Pop()
        {
            if (IsEmpty)
                throw new InvalidOperationException("Cannot Pop elements from an empty ImmutableStack");
            
            return (_tail, _head, _metadata);
        }

        public T Peek()
        {
            return _head;
        }
        
        public IEnumerator<T> GetEnumerator()
        {
            return new ImmutableStackEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return $"{Count} head: {_head}";
        }

        class ImmutableStackEnumerator : IEnumerator<T>
        {
            private ImmutableStack<T> _instance;
            private ImmutableStack<T> _originalInstance;
            private T _current;

            public ImmutableStackEnumerator(ImmutableStack<T> instance)
            {
                _instance = instance;
                _originalInstance = instance;
            }

            public bool MoveNext()
            {
                if (_instance.IsEmpty)
                    return false;
                
                (_instance, _current, _) = _instance.Pop();
                
                return true;
            }

            public void Reset()
            {
                _instance = _originalInstance;
            }

            public T Current => _current;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _instance = null;
                _originalInstance = null;
            }
        }
    }

    class ImmutableStack
    {
        public static ImmutableStack<T> Create<T>(T head, object metadata = null)
        {
            return new ImmutableStack<T>(new ImmutableStack<T>(), head, metadata);
        }
        
        public static ImmutableStack<T> Empty<T>()
        {
            return new ImmutableStack<T>();
        }
    }
}