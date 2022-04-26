using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public class AsyncTests : TestsBase
    {
        public async Task Test0()
        {
            Log.WriteLine(1);
            await Task.Delay(100);
        }

        public async Task Test1()
        {
            var start = DateTime.Now;
            var sum = 0;

            Log.WriteLine(1);
            await Delay(100);

            sum++;

            Log.WriteLine(2);
            await Delay(100);

            sum++;

            Log.WriteLine(3);
            await Delay(100);

            sum++;

            Log.WriteLine(4);

            if (sum != 3)
            {
                throw new Exception("AsyncTests.Test1 most likely failed the counting test: " + sum);
            }

            if (DateTime.Now - start < TimeSpan.FromMilliseconds(100))
                throw new Exception("AsyncTests.Test1 most likely failed the timing test: " +
                                    (DateTime.Now - start).TotalMilliseconds);
        }

        public async Task Test2()
        {
            var a = 1;

            if (a == 1)
            {
                await Delay(100);

                AssertEqual(a, 1);

                a++;
            }

            AssertEqual(a, 2);
        }

        public async Task Test3()
        {
            var a = 1;

            if (a == 1)
            {
                await Delay(1);

                AssertEqual(a, 1);

                a++;

                if (a == 2)
                {
                    await Delay(1);

                    AssertEqual(a, 2);

                    a++;
                }

                await Delay(1);

                AssertEqual(a, 3);

                a++;
            }

            AssertEqual(a, 4);
        }

        public async Task Test4()
        {
            var a = 0;

            for (int i = 0; i < 4; i++)
            {
                AssertEqual(a, i);
                await Delay(1);
                a++;
            }

            AssertEqual(a, 4);
        }

        public async Task Test5()
        {
            var result = await Task.FromResult(1);

            AssertEqual(result, 1);
        }

        public async Task Test6()
        {
            var result = await FromResultWithDelay(1);

            AssertEqual(result, 1);
        }

        public async Task Test7()
        {
            var s = "0";

            await Delay(1);

            AssertEqual(s, "0");

            s = "1";

            try
            {
                AssertEqual(s, "1");

                await Delay(1);

                s = "2";

                AssertEqual(s, "2");
            }
            catch
            {
                Assert(false);
            }

            s = "3";

            AssertEqual(s, "3");
        }

        public async Task<int> Test8()
        {
            Assert(true);

            var result2 = await FromResultWithDelay(1);
            var t = "a";

            if (true && t == "a")
                return 1;

            var result = await FromResultWithDelay(1);

            Assert(false);

            return 2;
        }

        public async Task Test9()
        {
            var i = 0;

            await Delay(1);

            try
            {
                i++;
                await Delay(1);
                i++;
                await Delay(1);
                i++;
                AssertEqual(i, 3);
            }
            catch
            {
                Assert(false);
            }

            await Delay(1);
            i++;

            try
            {
                await Delay(1);
                i++;
                AssertEqual(i, 5);
            }
            catch
            {
                Assert(false);
            }

            i++;

            AssertEqual(i, 6);
        }

        public async Task Test10()
        {
            var i = 0;

            await Delay(1);

            try
            {
                i++;
                await Delay(1);
                i++;
                AssertEqual(i, 2);

                try
                {
                    await Delay(1);
                    i++;
                    AssertEqual(i, 3);
                }
                catch
                {
                    Assert(false);
                }

                i++;
            }
            catch
            {
                Assert(false);
            }

            await Delay(1);
            i++;

            AssertEqual(i, 5);
        }

        public async Task Test11()
        {
            var s = "";

            await Delay(1);

            foreach (var c in new[] {'a', 'b', 'c'})
            {
                s += c;
            }

            AssertEqual(s, "abc");
        }

        public async Task Test12()
        {
            var s = "";

            foreach (var c in new[] {'a', 'b', 'c'})
            {
                s += c;
                await Delay(1);
            }

            AssertEqual(s, "abc");
        }

        public async Task Test13()
        {
            var i = 0;

            try
            {
                try
                {
                    AssertEqual(++i, 1);
                    await Delay(1);
                    AssertEqual(++i, 2);
                }
                catch
                {
                }
            }
            catch
            {
            }

            AssertEqual(++i, 3);
        }

        public async Task Test14()
        {
            var s = "";

            await Delay(1);

            foreach (var _ in new[] {'a', 'b', 'c'})
            {
                foreach (var cc in new[] {'a', 'b', 'c'})
                {
                    s += cc;
                    await Delay(1);
                }
            }

            AssertEqual(s, "abcabcabc");
        }

        public async Task Test15()
        {
            await Delay(1).ConfigureAwait(false);
        }

        public async Task Test16()
        {
            var func = new Func<Task>(async () => await Delay(1).ConfigureAwait(false));

            await func();
        }

        public void Test17()
        {
            var func = new Func<Task>(async () => await Delay(1).ConfigureAwait(false));

            func().Wait();
        }

        public async Task Test18()
        {
            var result = await TestMethod();

            AssertEqual(result, "return value");
        }

        public async Task<List<int>> Test19()
        {
            return await FromResultWithDelay(new List<int>());
        }

        public async Task<List<int>> Test20() =>
            await FromResultWithDelay(new List<int>());
        
        public async Task Test21()
        {
            bool result = false;
            try {
                throw new TestException();
            } catch (TestException exception) {
                await Delay(0);
                result = exception != null;
            }

            AssertEqual(result, true);
        }

        /// <summary>
        /// We need a local Delay method for logging
        /// </summary>
        private static Task Delay(int milliseconds)
        {
            Log.WriteLine("Delay called");
            return Task.Delay(milliseconds);
        }

        private static async Task<T> FromResultWithDelay<T>(T val) where T : new()
        {
            await Delay(1);
            return val;
        }

        Task<string> TestMethod(CancellationToken token = default)
        {
            return Task.FromResult("return value");
        }

        class TestException : Exception {}
    }
}