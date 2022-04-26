using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public class LambdaTests : TestsBase
    {
        public void Test0()
        {
            int i = 0;

            InvokeAction(() => i = 1);

            AssertEqual(i, 1);
        }

        public void Test1()
        {
            int i = 0;

            i++;

            Mutate();

            i++;

            void Mutate()
            {
                i++;
            }

            AssertEqual(i, 3);
        }

        /// <summary>
        /// Test variable scope intersection
        /// </summary>
        public void Test2()
        {
            Func<double, double> f0 = val => val;
            
            Func<double, double> f1 = val => val;

            AssertEqual(f1(5), 5);
        }

        public void Test3()
        {
            var startValue = StartValue();
        }
        
        double StartValue() => 0;

        public void Test30()
        {
            acceptFunc(() => {
                return 1;
            });

            void acceptFunc(Func<double> func)
            {

            }
        }

        private void InvokeAction(Action action) => action();
    }
}
