using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public class SwitchTests : TestsBase
    {
        public void Test0()
        {
            var i = 5;

            switch (i) {
                case 1: Assert(false); break;
                case 5: Assert(true); break;
                default: Assert(false); break;
            }
        }
        public void Test1()
        {
            var i = 5;

            switch (i) {
                case 1: Assert(false); return;
                case 2: Assert(false); return;
                default: Assert(true); return;
            }
        }
    }
}
