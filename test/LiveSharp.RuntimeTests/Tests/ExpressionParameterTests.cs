using System;
using System.Linq.Expressions;

namespace LiveSharp.RuntimeTests
{
    public class ExpressionParameterTests : TestsBase
    {
        public void Test0()
        {
            var result = (double)Method(() => 2);
            
            AssertEqual((int)result, 2);
        }

        public void Test1()
        {
            var result = (string)Method2(c => c.A);
            
            AssertEqual(result, "A");
        }

        public void Test2()
        {
            var s = "somestring";

            Method(() => s.Length == 2 ? 1.0 : 0.0);
        }
        
        object Method(Expression<Func<double>> f)
        {
            return f.Compile().Invoke();
        }
        
        object Method2(Expression<Func<TestClass, string>> f)
        {
            return f.Compile().Invoke(new TestClass());
        }
        
        class TestClass
        {
            public string A => "A";
        }
    }
}