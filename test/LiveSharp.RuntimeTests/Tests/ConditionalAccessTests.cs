using System;
using System.IO;

namespace LiveSharp.RuntimeTests
{
    public class ConditionalAccessTests : TestsBase
    {
        public void Test0()
        {
            var i = "."?.Length;
            var s = "."?.ToString().ToString().ToString()?.ToString();
        
            AssertEqual(s, ".");
        }
        
        public void Test1()
        {
            var s = "."?.Length;
        
            AssertEqual(s, 1);
        }
        
        public void Test2()
        {
            var s = "."?.ToString();
        
            AssertEqual(s, ".");
        }
        
        public void Test3()
        {
            string a = null;
            var s = a?.Length;
        
            AssertEqual(s.HasValue, false);
        }
        
        public void Test4()
        {
            var tc = new TestClass();
            tc?.MutateValue();
        
            AssertEqual(tc.Value, 42);
        }

        public void Test5()
        {
            //var n = new Nullable<bool>(true);
            var nullableBool = (bool?)true;
            var result = nullableBool?.ToString();

            AssertEqual(result, true.ToString());
        }

        public void Test6()
        {
            var ts = new TestClass(25);
            var a = ts?.Value;
            
            AssertEqual(a.Value, 25);
        }

        public void Test7()
        {
            var a = new TestClass(25)?.StringValue?.ToString();
        }

        public void Test8()
        {
            TestClass tc = null;
            var a = tc?.Value.ToString() ?? "Connecting";
            
            AssertEqual(a, "Connecting");
        }

        struct TestStruct
        {
            public int Bar;
        }
        
        class TestClass
        {
            public int Value { get; private set; }
            public string StringValue { get; private set; }
            
            public TestClass()
            {
            }

            public TestClass(int value)
            {
                Value = value;
                StringValue = value.ToString();
            }

            public void MutateValue()
            {
                Value = 42;
            }
        }
    }
}