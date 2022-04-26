using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSharp.RuntimeTests
{
    public class WhileTests : TestsBase
    {
        public void Test0()
        {
            int k = 5;

            while (k > 0)
                k--;

            AssertEqual(k, 0);
        }

        public void Test1()
        {
            int k = 5;

            while (k > 0) {
                if (k > 2) k--;
                else break;
            }

            AssertEqual(k, 2);
        }

        public void Test2()
        {
            int k = 10;
            int counter = 0;

            while (k > 0) {
                k--;
                if (k % 2 == 0)
                    continue;
                counter++;
            }

            AssertEqual(counter, 5);
        }
    }
}