using System;
using System.Diagnostics;

namespace LiveSharp.RuntimeTests
{
    public class UsingTests : TestsBase
    {
        public void Test0()
        {
            DisposableDummy copy;

            using (var d = new DisposableDummy()) {
                copy = d;
            }

            AssertEqual(copy.IsDisposed, true);
        }

        public void Test1()
        {
            var isDisposed = false;

            using (new DisposableDummy(() => isDisposed = true)) {}

            AssertEqual(isDisposed, true);
        }

        public void Test2()
        {
            var isDisposed = false;
            var dummy = new ValueDisposableDummy?(new ValueDisposableDummy(() => isDisposed = true));

            using (dummy) {}

            AssertEqual(isDisposed, true);
        }

        public void Test4()
        {
            // check syntax
            using var d = new DisposableDummy();
            
            AssertEqual(d.IsDisposed, false);
        }

        class DerivedDisposableDummy : DisposableDummy
        {
            public DerivedDisposableDummy(Action onDispose = null) : base(onDispose)
            {}
        }

        class DisposableDummy : IDisposable
        {
            public bool IsDisposed { get; private set; }

            private readonly Action _onDispose;

            public DisposableDummy(Action onDispose = null)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                IsDisposed = true;
                _onDispose?.Invoke();
            }
        }

        struct ValueDisposableDummy : IDisposable
        {
            private readonly Action _onDispose;
            
            public ValueDisposableDummy(Action onDispose = null)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                _onDispose?.Invoke();
            }
        }
    }
}