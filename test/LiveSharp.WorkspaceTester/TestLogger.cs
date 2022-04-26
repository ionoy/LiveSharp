using System;

namespace LiveSharp.WorkspaceTester
{
    class TestLogger : ILogger
    {
        public void LogError(string errorMessage)
        {
            Console.WriteLine("error: " + errorMessage);
        }

        public void LogError(string errorMessage, Exception e)
        {
            Console.WriteLine("error: " + errorMessage + Environment.NewLine + e);
        }

        public void LogMessage(string message)
        {
            Console.WriteLine(message);
        }

        public void LogWarning(string warning)
        {
            Console.WriteLine("warning: " + warning);
        }
    }
}