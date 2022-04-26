using System;
using LiveSharp.Ide.Infrastructure;

namespace LiveSharp.RuntimeTests.Tests
{
    public class StringTests : TestsBase
    {
        public void Test10 ()
        {
            var s = $"abc{1}";
            AssertEqual(s, "abc1");
        }
        
        public void Test20 ()
        {
            var s = $@"abc{1}";
            AssertEqual(s, "abc1");
        }
        
        public void Test30 ()
        {
            var s = new string(" ");
            AssertEqual(s.Length, 1);
        }
    }
}