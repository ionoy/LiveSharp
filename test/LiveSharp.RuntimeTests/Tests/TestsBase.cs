using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LiveSharp.RuntimeTests
{
    public abstract class TestsBase
    {
        public int AssertCallCount { get; private set; }

        protected void Assert(bool condition, string message = null, [CallerMemberName] string caller = null)
        {
            AssertCallCount++;
            Log.WriteLine($"{message + " ; "}Assertion at {caller}: " + condition);
            if (!condition) {
                Log.WriteLine($"Assertion failed at {caller}: {message}");
                throw new AssertionException($"Assertion failed at {caller}: {message}");
            }
        }

        protected void AssertEqual<TArg>(TArg actual, TArg expected, string message = null,
            [CallerMemberName] string caller = null)
        {
            AssertCallCount++;
            Log.WriteLine($"{message + " ; "}Equality assertion at {caller}. Expected {expected}, actual {actual}");

            if (!EqualityComparer<TArg>.Default.Equals(actual, expected))
                throw new AssertionException(
                    $"{message + " ;"}Equality assertion failed at {caller}. Expected {expected}, actual {actual}");
        }

        public virtual bool TestOverride()
        {
            return true;
        }

        public class AssertionException : Exception
        {
            public AssertionException(string message) : base(message)
            {
            }
        }
    }
}