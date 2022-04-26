using System;
using System.Reflection;
using System.Runtime.Loader;

namespace LiveSharp.Workbench
{
    class Program
    {
        static void Main(string[] args)
        {
            var agent = new Agent { Id = 1 };
            Console.WriteLine("Hello World!");
        }
    }

    public record Agent
    {
        public int Id { get; init; }
    }
}