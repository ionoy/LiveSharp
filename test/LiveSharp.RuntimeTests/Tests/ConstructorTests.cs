using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public class ConstructorTests : TestsBase
    {
        class TestDummy
        {
            public int Value = -1;
            public TestDummy(int a = 0)
            {
                Value = a;
            }
        }

        public void Test0()
        {
            var a = new TestDummy(1);

            AssertEqual(a.Value, 1);
        }
        public void Test1()
        {
            var a = new TestDummy() { };

            AssertEqual(a.Value, 0);
        }
        public void Test2()
        {
            var a = new TestDummy { };

            AssertEqual(a.Value, 0);
        }
    }
}
