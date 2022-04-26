using System;

namespace LiveSharp.RuntimeTests
{
    class TestLogger : ILogger, LiveSharp.Runtime.ILogger
    {
        public bool IsDebugLoggingEnabled { get; set; }

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

        public void LogDebug(string debugInfo)
        {
            if (IsDebugLoggingEnabled)
                Console.WriteLine("debug: " + debugInfo);
        }
    }
}