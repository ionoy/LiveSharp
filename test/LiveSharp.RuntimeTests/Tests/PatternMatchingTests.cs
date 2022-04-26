using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSharp.RuntimeTests
{
    public class PatternMatchingTests : TestsBase
    {
        public void Test0()
        {
            object k = 5;

            if (k is int) {
                Assert(true);
            } else {
                Assert(false);
            }
        }
        public void Test1()
        {
            object o = 5;

            if (o is int i) {
                AssertEqual(i, 5);
            } else {
                Assert(false);
            }
        }
        public void Test2()
        {
            object o = 5;

            if (o is var i && (int)i == 5) {
                Assert(true);
            } else {
                Assert(false);
            }
        }

        public void Test3()
        {
            object o = "a";

            switch (o) {
                case int i:
                    Assert(false);
                    break;
                case bool b:
                    Assert(false);
                    break;
                case string s:
                    AssertEqual(s, "a");
                    break;
                default:
                    Assert(false);
                    break;
            }
        }
        public void Test5()
        {
            object o = 5;

            if (true && o is var i && (int)i == 5) {
                Assert(true);
            } else {
                Assert(false);
            }
        }

        public void Test6()
        {
            object o = 5;

            if (o is int _)
            {
                Assert(true);
            }
            else
            {
                Assert(false);
            }
        }

        //public void Test7()
        //{
        //    var o = ("s", 5, true);

        //    if (o is (var s, 5, bool b))
        //    {
        //        AssertEqual(s, "s");
        //        //AssertEqual(i, 5);
        //        AssertEqual(b, true);
        //    }
        //    else
        //    {
        //        Assert(false);
        //    }
        //}

        //public void Test8()
        //{
        //    object o = ("s", 5);

        //    if (o is (string s, int i)) {
        //        AssertEqual(s, "s");
        //        AssertEqual(i, 5);
        //    } else {
        //        Assert(false);
        //    }
        //}

        //public void Test9()
        //{
        //    Deconstructable o = new Deconstructable();

        //    if (o is (var s, var i)) {
        //        AssertEqual(s, "s");
        //        AssertEqual(i, 5);
        //    } else {
        //        Assert(false);
        //    }
        //}

        //public void Test10()
        //{
        //    Deconstructable o = new Deconstructable();

        //    if (o is (_, var i)) {
        //        AssertEqual(i, 5);
        //    } else {
        //        Assert(false);
        //    }
        //}

        //public void Test11()
        //{
        //    object o = ("s", (5, true));

        //    if (o is (string s, (var i, bool b))) {
        //        AssertEqual(s, "s");
        //        AssertEqual(i, 5);
        //        AssertEqual(b, true);
        //    } else {
        //        Assert(false, "ITuple deconstruct failed");
        //    }
        //}

        //public void Test12()
        //{
        //    DeconstructableStruct o = new DeconstructableStruct();

        //    if (o is (string s, var i)) {
        //        AssertEqual(s, "s");
        //        AssertEqual(i, 5);
        //    } else {
        //        Assert(false, "Deconstruct failed");
        //    }
        //}

        class Deconstructable
        {
            public void Deconstruct(out string s, out int i)
            {
                s = "s";
                i = 5;
            }
        }

        class DeconstructableStruct
        {
            public void Deconstruct(out string s, out int i)
            {
                s = "s";
                i = 5;
            }
        }
    }
}