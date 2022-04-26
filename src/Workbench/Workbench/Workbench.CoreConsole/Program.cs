using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Workbench.Library;

namespace LiveSharpCoreConsoleSample
{
    class Program
    {
        private static bool? result = false;
        static void Main(string[] args)
        {
            if (result == true) {
                Console.WriteLine("TRUE1");
            }

            var libraryType = new LibraryType();
            var a = 1;
            var b = 2;
            var c = 3;
            libraryType.LibraryMethod(a, b, c);
            A<string>.LaLa();
            Console.ReadKey();
        }

        class A<T>
        {
            public static void LaLa()
            {}
        }
        
        static void FizzBuzz()
        {
            for (int i = 0; i < 2; i++)
            {
                var fizz = i % 3 == 0;
                var buzz = i % 5 == 0;

                if (fizz && buzz)
                    Console.WriteLine("FizzBuzz");
                else if (fizz)
                    Console.WriteLine("Fizz");
                else if (buzz)
                    Console.WriteLine("Buzz");
            }
        }
    }

}
