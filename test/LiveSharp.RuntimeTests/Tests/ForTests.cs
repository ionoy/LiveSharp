using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSharp.RuntimeTests
{
    public class ForTests : TestsBase
    {
        public void Test0()
        {
            int k = 0;
        
            for (int i = 0; i < 10; i++)
                k += 1;
        
            AssertEqual(k, 10);
        }
        
        public void Test01()
        {
            int k = 0;
        
            for (int i = 1; i < 10; i++)
                k += 1;
        
            AssertEqual(k, 9);
        }
        
        public void Test02()
        {
            int k = 0;
        
            for (int i = 0; i < 10; i += 2)
                k += 1;
        
            AssertEqual(k, 5);
        }
        
        public void Test03()
        {
            int k = 0;

            for (int i = 0, j = 0; i < 10; i++, j++) {
                k += 1;
                
                AssertEqual(i, j);
            }

            AssertEqual(k, 10);
        }

        public void Test1()
        {
            int k = 0;

            for (; k < 10; k++)
                k += 1;

            AssertEqual(k, 10);
        }

        public void Test2()
        {
            int k = 0;

            for (;; k++) {
                if (k == 10)
                    break;
            }

            AssertEqual(k, 10);
        }

        public void Test3()
        {
            int k = 0;

            for (;;) {
                k += 1;

                if (k == 10)
                    break;
            }

            AssertEqual(k, 10);
        }

        public void Test4()
        {
            int k = 0;
            int i = 0;

            for (; i < 10; i++) {
                if (i != 7)
                    continue;

                k = i;
            }
            
            AssertEqual(k, 7);
            AssertEqual(i, 10);
        }

        //public void Test5()
        //{
        //    var array = new [] { 0, 1, 2, 3 };
        //    var sum = 0;

        //    foreach (var i in array)
        //        sum += i;

        //    AssertEqual(sum, 6);
        //}

        //public void Test6()
        //{
        //    var stack = new Stack<int>(new [] { 0, 1, 2, 3 });
        //    var sum = 0;

        //    foreach (var i in stack)
        //        sum += i;

        //    AssertEqual(sum, 6);
        //}
    }
}