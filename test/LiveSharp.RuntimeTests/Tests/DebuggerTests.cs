using System;

namespace LiveSharp.RuntimeTests.Tests
{
    public class DebuggerTests : TestsBase
    {
        private bool _isSuccess = false;

        public void Test0()
        {
            var local = "z";
            
            ReturnInt("aaa");

            if (ReturnInt("b") == 2) {
                local = "true";
                _isSuccess = true;
                Console.WriteLine("IsSuccess!");
            } 
            else
            {
                local = "false";
                _isSuccess = false;
                Console.WriteLine("Not much of a success now is it?");
            }

            foreach (var i in new[] { "a", "b", "c" }) {
                local = i;
                Console.WriteLine(ReturnInt(i));
            }
        }

        public int ReturnInt(string input)
        {
            return input.Length * 2;
        }
    }
}