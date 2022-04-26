using System;
using System.Linq.Expressions;

namespace LiveSharp.Runtime.IL
{
    public class IlStack
    {
        private readonly object[] _stack;
        private int _size;

        public IlStack(int maxStackSize)
        {
            _stack = new object[maxStackSize];
        }

        public void Push(object expr)
        {
            _stack[_size++] = expr;
        }

        public object Pop()
        {
            return _stack[--_size];
        }

        public void PopValues(int count)
        {
            _size -= count;
        }

        public object Get(int indexFromLast)
        {
            return _stack[_size - indexFromLast - 1];
        }

        private Expression PushValueStack(Expression value, IlStack stack)
        {
            Expression<Action> push = () => stack.Push(value);
            return push.Body;
        }

        private Expression PopValue(IlStack stack)
        {
            Expression<Func<object>> pop = () => stack.Pop();
            return pop.Body;
        }
        
        private Expression PopValuesStack(int count, IlStack stack)
        {
            Expression<Action> pop = () => stack.PopValues(count);
            return pop.Body;
        }

        private Expression GetValueStack(int indexFromLast, IlStack stack)
        {
            Expression<Func<object>> get = () => stack.Get(indexFromLast);
            return get.Body;
        }
    }
}