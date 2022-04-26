using System;

namespace LiveSharp.RuntimeTests
{
    public class DelegateTests : TestsBase
    {
        delegate double FuncDouble ();

        public void Test0()
        {
            var func = new Func<double>(() => 1.0);
        }

        public void Test1()
        {
            var func = new FuncDouble(() => 1.0);
        }

        public void Test2()
        {
            var func = new EventHandler((sender, args) => { });
        }
    }
}