using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleSample
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Method();
                Thread.Sleep(1000);
            }
        }

        static void Method()
        {
            Console.WriteLine("Hello, Misha!");
        }
    }
}
