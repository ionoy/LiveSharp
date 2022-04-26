using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSharp.RuntimeTests
{
    public class ForeachTests : TestsBase
    {
        public void Test0()
        {
            var c = new[] { 1, 2, 3 };
            var sum = 0;

            foreach (var v in c) {
                sum += v;
            }

            AssertEqual(sum, 6);
        }

        public void Test1()
        {
            var c = new List<int> { 1, 2, 3 };
            var sum = 0;

            foreach (var v in c) {
                sum += v;
            }

            AssertEqual(sum, 6);
        }

        public void Test2()
        {
            var c = new Dictionary<string, int> {{ "one", 1 }, { "two", 2 }, { "three", 3 } };
            var sum = 0;

            foreach (KeyValuePair<string, int> v in c) {
                sum += v.Value;
            }

            AssertEqual(sum, 6);
        }

        public void Test3()
        {
            var c = new[] { 1, 2, 3 };
            var sum = 0;

            foreach (var v in c) {
                sum += v;

                if (v == 2)
                    break;
            }

            AssertEqual(sum, 3);
        }

        public void Test4()
        {
            var c = new[] { 1, 2, 3 };
            var sum = 0;

            foreach (var v in c) {
                if (v == 2)
                    continue;

                sum += v;
            }

            AssertEqual(sum, 4);
        }

        public void Test5()
        {
            var c = "abc";
            var result = "";

            foreach (char v in c) {
                result = v + result;
            }

            AssertEqual(result, "cba");
        }
        public void Test51()
        {
            var s = "abc";
            var result = "";

            foreach (var v in s) {
                result += v;
            }

            AssertEqual(result, "abc");
        }

        public void Test6()
        {
            var enumerable = new Enumerable();
            var sum = 0;

            foreach (var e in enumerable) {
                sum += e;
            }
          
            AssertEqual(sum, 42);
        }

        // Dynamic not supported yet
        //public void Test7()
        //{
        //    dynamic enumerable = new Enumerable();
        //    var sum = 0;

        //    foreach (var e in (Enumerable)enumerable) {
        //        sum += e;
        //    }
            
        //    AssertEqual(sum, 42);
        //}

        public void Test8()
        {
            var c = new[] { 1, 2, 3 };
            var sum = 0;

            foreach (int e in (IEnumerable)c) {
                sum += e;
            }
            
            AssertEqual(sum, 6);
        }
        
        // ForEachVariableStatement not supported yet
        /*public void Test9()
        {
            var c = new[] {(1, ""), (2, ""), (3, "") };
            var sum = 0;

            foreach (var (i, _) in c) {
                sum += i;
            }
            
            AssertEqual(sum, 6);
        }

        public void Test10()
        {
            var c = new[] {((1, ""), ""), ((2, ""), ""), ((3, ""), "") };
            var sum = 0;

            foreach (var ((i, _), _) in c) {
                sum += i;
            }
            
            AssertEqual(sum, 6);
        }*/

        public void Test11()
        {
            var handlers = new List<Action>();
            var items = new object[] {0, 1, 2};
            var sum = 0;
            
            foreach (var i in items) {
                handlers.Add(() => sum += (int)i);
            }

            foreach (var handler in handlers) handler();

            AssertEqual(sum, 3);
        }

        #region Helpers

        class Enumerable
        {
            public Enumerator GetEnumerator() => new Enumerator();
        }

        class Enumerator
        {
            private bool _sequenceEnded;

            public int Current {
                get {
                    _sequenceEnded = true;
                    return 42;
                }
            }

            public bool MoveNext() => !_sequenceEnded;
        }

        #endregion
    }
}