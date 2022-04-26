using System;

namespace LiveSharp.RuntimeTests.Sequences.ChangeReturnValue
{
    public class ChangeReturnValue : IRunnableSequence
    {
        public void Run()
        {
            Console.WriteLine(ReturnValue());
        }
        private int ReturnValue() => 5;
    }
}