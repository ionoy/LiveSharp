using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public class ReturnTests : TestsBase
    {
        public void Test0()
        {
            Assert(true);

            if (true)
                return;

            Assert(false);
        }
    }
}
