using System;
using System.Diagnostics;

namespace LiveSharp.RuntimeTests
{
    public class Log
    {
        public static void WriteLine(string line)
        {
            Console.WriteLine(line);
            Debug.WriteLine(line);
        }
        public static void WriteLine(object line)
        {
            Console.WriteLine(line);
            Debug.WriteLine(line);
        }
    }
}