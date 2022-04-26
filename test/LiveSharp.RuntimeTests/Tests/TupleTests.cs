using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace LiveSharp.RuntimeTests
{
    public class TupleTests : TestsBase
    {
        public void Test0()
        {
            AssertEqual(TakesExactlyOneTwo((1, 2)), true);
        }
        
        public void Test1()
        {
            var (a, b) = ReturnInts();
        
            AssertEqual(a, 1);
            AssertEqual(b, 2);
        }
        
        public void Test2()
        {
            var (i, (s, b)) = ReturnNested();
        
            AssertEqual(i, 1);
            AssertEqual(s, "a");
            AssertEqual(b, false);
        }
        
        public void Test3()
        {
            var (i, (_, _)) = ReturnNested();
        
            AssertEqual(i, 1);
            //AssertEqual(b, false);
        }
        
        public void Test4()
        {
            Assert(TakesEnums((BindingFlags.GetField, BindingFlags.GetProperty)));
        }

        public static bool TakesEnums<TEnum>(params (TEnum first, BindingFlags second)[] cols) where TEnum : IConvertible
        {
            return cols.All(t => (BindingFlags)((object)t.first) == BindingFlags.GetField && t.second == BindingFlags.GetProperty);
        }

        bool TakesExactlyOneTwo((int, int) t)
        {
            Log.WriteLine(t);
            return t.Item1 == 1 && t.Item2 == 2;
        }

        (int, int) ReturnInts() => (1,2);
        (int, (string, bool)) ReturnNested() => (1, ("a", false));
    }
}