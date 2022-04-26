using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSharp.RuntimeTests
{
    public class DoTests : TestsBase
    {
        public void Test0()
        {
            int k = 5;

            do {
                k--;
            } while (k > 0);
                

            AssertEqual(k, 0);
        }

        public void Test1()
        {
            int k = 5;

            do {
                if (k > 2) k--;
                else break;
            } while (k > 0);

            AssertEqual(k, 2);
        }

        public void Test2()
        {
            int k = 10;
            int counter = 0;
            
            do {
                k--;
                if (k % 2 == 0)
                    continue;
                counter++;
            } while (k > 0);
            
            AssertEqual(counter, 5);
        }

        public void Test3()
        {
            int k = 10;
            
            do {
                break;
                k--;
            } while (k-- > 0);
            
            AssertEqual(k, 10);
        }
    }
}