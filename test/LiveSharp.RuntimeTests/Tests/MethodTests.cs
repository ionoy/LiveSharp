using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public class MethodTests : TestsBase
    {
        public int SideEffectInt { get; set; }
        public string SideEffectString { get; set; }

        public MethodTests Inner { get; set; }

        public MethodTests()
        {
            Inner = new MethodTests(false);
        }

        public MethodTests(bool createInner)
        {
            if (createInner)
                Inner = new MethodTests(false);
        }

        public void Test01()
        {
            VoidMethodNoArgs();
        }

        public void Test1()
        {
            Inner.VoidMethodNoArgs();
        }

        public void Test2()
        {
            var inner = Inner;
            inner.VoidMethodNoArgs();
        }

        public void Test3()
        {
            VoidMethod(32);
        }

        public void Test4()
        {
            VoidMethod(32, "s");
        }

        public void Test5()
        {
            VoidMethodDefaultValue(32);
        }

        public void Test6()
        {
            VoidMethodDefaultValue(32, "non-default");
        }

        public void Test7()
        {
            VoidMethodDefaultValue(32, s: "named arg");
        }

        public void Test8()
        {
            VoidMethodParams(2, 4, 8);
        }

        public void Test81()
        {
            VoidMethodParams();
        }

        public void Test9()
        {
            VoidMethodParams(2, 4, 8);
        }

        public void Test10()
        {
            VoidMethodStatic(this);
        }

        public void Test11()
        {
            InnerClass.VoidMethodStatic(this);
        }

        public void Test12()
        {
            GenericMethodNoArgs<MethodTests>();
        }

        public void Test13()
        {
            int i;
            VoidMethodOutParam(out i);

            AssertEqual(i, 2);
        }

        public void Test14()
        {
            VoidMethodOutParam(out int i);

            AssertEqual(i, 2);
        }

        public void Test15()
        {
            VoidMethodOutParam(out var i);

            AssertEqual(i, 2);
        }

        public void Test16()
        {
            MethodWithDefault();

            AssertEqual(SideEffectString, "default2");
        }

        IInnerClass InnerClassProperty => new InnerClass();

        public void Test17()
        {
            InnerClassProperty.MethodWithDefault(this);

            AssertEqual(SideEffectString, "default2");
        }

        public void Test18()
        {
            MethodWithDefault(default);

            AssertEqual(SideEffectString, "default2");
        }

        public void Test19()
        {
            MethodWithDefault(default(CancellationToken));

            AssertEqual(SideEffectString, "default2");
        }

        // Non-test methods
        public void VoidMethodNoArgs()
        {
            Assert(true);
        }

        public void VoidMethod(int i)
        {
            Assert(true);
        }

        public void VoidMethod(int i, string s)
        {
            SideEffectInt = i;
            SideEffectString = s;

            Assert(true);
        }

        public void VoidMethodDefaultValue(int i, string s = "default")
        {
            SideEffectInt = i;
            SideEffectString = s;

            Assert(true);
        }

        public void VoidMethodParams(params int[] args)
        {
            SideEffectInt = args != null && args.Length > 0 ? args.Sum() : -1;

            Assert(true);
        }

        public void VoidMethodOutParam(out int i)
        {
            i = 2;

            Assert(true);
        }

        public void GenericMethodNoArgs<T>()
        {
            SideEffectString = typeof(T).Name;

            Assert(true);
        }

        public static void VoidMethodStatic(MethodTests obj)
        {
            obj.SideEffectInt = 16;
        }

        public void MethodWithDefault(CancellationToken s = default)
        {
            SideEffectString = "default2";
        }

        interface IInnerClass
        {
            void MethodWithDefault(MethodTests obj, CancellationToken s = default);
        }

        class InnerClass : IInnerClass
        {
            public static void VoidMethodStatic(MethodTests obj)
            {
                obj.SideEffectInt = 64;
            }

            public void MethodWithDefault(MethodTests obj, CancellationToken token = default)
            {
                obj.SideEffectString = "default2";
            }
        }
    }
}