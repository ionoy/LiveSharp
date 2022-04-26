using LiveSharp.Runtime.Virtual;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xamarin.Forms;

namespace LiveSharp.RuntimeTests.Tests
{
    public class BasicTests : TestsBase
    {
        private readonly Dictionary<string, int> _dictionary = new Dictionary<string, int>();

        public void Test0()
        {
            System.Boolean b = true;
        }
        
        public void Test1()
        {
            System.Boolean b = true;
            AssertEqual(b, true);
        }
        
        public void Test2()
        {
            var arr = new int[] {
                1, 2, 3
            };
        
            AssertEqual(arr.Length, 3);
            AssertEqual(arr[0], 1);
            AssertEqual(arr[1], 2);
            AssertEqual(arr[2], 3);
        }
        
        public void Test3()
        {
            _dictionary["a"] = 128;
            AssertEqual(_dictionary["a"], 128);
        }
        
        public int Test4()
        {
            return 1;
            AssertEqual(true, false);
        }
        
        public void Test5()
        {
            return;
            AssertEqual(true, false);
        }
        
        public void Test6()
        {
            var detailStack = new StackLayout 
            { 
                Orientation = StackOrientation.Vertical, 
                HorizontalOptions = LayoutOptions.StartAndExpand, 
                Margin = new Thickness(0, 8, 0, 0)
            };
        }

        public void Test7()
        {
            string s = "";
            char c = 'c';

            s += c;

            AssertEqual(s, "c");
        }

        public void Test8()
        {
            this._dictionary["this"] = 3;

            AssertEqual(this._dictionary["this"], 3);
        }

        public void Test9()
        {
            this._dictionary["base"] = 4;

            base.AssertEqual(this._dictionary["base"], 4);
        }

        public void Test10()
        {
            var enums = new E[] {E.Two, E.One};
        }
        
        public void Test20()
        {
            var flag0 = true; 
            Debug.WriteLine("Test debug string");
            AssertEqual(flag0, true);
            //var a = new Aa();
            //var b = new A<string>();
            //var c = new A<string>.B<int>();
            //var e = new A<int>.B<string>.C();
        
            //AssertEqual(e.One, default(int));
            //AssertEqual(e.Two, default(string));
        }
        
        public void Test28()
        {
            var a = "";
            var b = "null";

            if (b == "") {
                string.Format(a, b);
            } else {
                string.Format(a, b);
            }
        }
        
        public void Test29()
        {
            var f = new Func<int>(() => 2);
        }

        public void Test30()
        {
            var a = "a";
            var b = a.Where(i => i < 3);
        }

        enum E
        {
            One = 1,
            Two = 2
        }
        
        class Aa
        {}

        class A<T1>
        {
            public class B<T2>
            {
                public class C
                {
                    public T1 One => default (T1);
                    public T2 Two => default (T2);
                }
            }
        }
    }
}
