using System;
using System.Diagnostics;
using Xamarin.Forms;

namespace LiveSharp.RuntimeTests
{
    public class OperatorTests : TestsBase
    {
        public int Mod => 7;

        public void Test9()
        {
            var a = DateTimeKind.Local | DateTimeKind.Unspecified;

            Assert(a.HasFlag(DateTimeKind.Local));
        }

        public void Test0()
        {
            var rnd = new Random();
            var val = new Point(rnd.Next() % Mod, rnd.Next() % Mod);

            Log.WriteLine(val.X + ", " + val.Y);
        }

        public void Test1()
        {
            var a = (1 + 2) * 3;

            AssertEqual(a, 9);
        }

        public void Test2()
        {
            var a = 10.0 - 1;

            AssertEqual(a, 9.0);
        }

        public void Test3()
        {
            var tc = new TestClass(10);
            var tc2 = tc * 5;
            var tc3 = 5 * tc;

            AssertEqual(tc2.Value, 50);
            AssertEqual(tc3.Value, 50);
        }

        public void Test4()
        {
            double a = 1;
            int b = 2;

            AssertEqual(a >= b, false);
            AssertEqual(a <= b, true);
            AssertEqual(a < b, true);
            AssertEqual(a > b, false);

            AssertEqual(b >= a, true);
            AssertEqual(b <= a, false);
            AssertEqual(b < a, false);
            AssertEqual(b > a, true);

            AssertEqual(b == a, false);
            AssertEqual(b != a, true);
        }

        public void Test5()
        {
            double a = 1;
            int? b = 2;

            AssertEqual(a >= b, false);
            AssertEqual(a <= b, true);
            AssertEqual(a < b, true);
            AssertEqual(a > b, false);

            AssertEqual(b >= a, true);
            AssertEqual(b <= a, false);
            AssertEqual(b < a, false);
            AssertEqual(b > a, true);

            AssertEqual(b == a, false);
            AssertEqual(b != a, true);
        }

        public void Test6()
        {
            bool a = true;
            bool b = false;

            AssertEqual(a == b, false, "1");
            AssertEqual(a != b, true, "2");
            AssertEqual(a |= false, true, "3");
            a = true;
            AssertEqual(a |= true, true, "4");
            a = true;
            AssertEqual(a &= false, false, "5");
            a = true;
            AssertEqual(a &= true, true, "6");
        }

        public void Test7()
        {
            bool a = true;
            bool? b = false;
            
            AssertEqual(a == b, false, "1");
            AssertEqual(a != b, true, "2");
            AssertEqual(a |= false, true, "3");
            a = true;
            AssertEqual(a |= true, true, "4");
            a = true;
            AssertEqual(a &= false, false, "5");
            a = true;
            AssertEqual(a &= true, true, "6");
        }

        public void Test8()
        {
            double a = 1;

            a += 2;

            AssertEqual(a, 3);
        }

        public void Test10()
        {
            ushort sum1 = 0;
            ushort sum2 = 0;
            
            var r = (ushort) ((sum2 << 8) | sum1);
        }
        
        class TestClass
        {
            public int Value { get; }

            public TestClass(int value)
            {
                Value = value;
            }

            public static TestClass operator *(TestClass a, int v)
            {
                return new TestClass(a.Value * v);
            }

            public static TestClass operator *(int v, TestClass a)
            {
                return new TestClass(a.Value * v);
            }
        }
    }
}