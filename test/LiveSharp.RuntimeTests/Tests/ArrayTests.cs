using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public class ArrayTests : TestsBase
    {
        public void Test0()
        {
            var aa = new int[2];

            AssertEqual(aa.Length, 2);
        }
        public void Test1()
        {
            var aa = new int[3, 4];

            AssertEqual(aa.Rank, 2);
            AssertEqual(aa.Length, 12);
        }
        public void Test3()
        {
            //int[][] jaggedArray = new int[3][];

            //jaggedArray[0] = new int[5];
            //jaggedArray[1] = new int[4];
            //jaggedArray[2] = new int[2];
        }
    }
}
