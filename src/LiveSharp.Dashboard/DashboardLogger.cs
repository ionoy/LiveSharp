using System;
using System.IO;
using System.Threading.Tasks;

namespace LiveSharp.Dashboard
{
    public class DashboardLogger : ILogger
    {
        public event EventHandler<string> LogAppended;

        public DashboardLogger()
        {
        }

        public bool IsDebugLoggingEnabled { get; set; }

        public void LogError(string errorMessage)
        {
            WriteLine(errorMessage);
        }

        public void LogError(string errorMessage, Exception e)
        {
            WriteLine(errorMessage + Environment.NewLine + e);
        }

        public void LogMessage(string message)
        {
            WriteLine(message);
        }

        public void LogWarning(string warning)
        {
            WriteLine(warning);
        }

        public void LogDebug(string debugInfo)
        {
            if (IsDebugLoggingEnabled)
                WriteLine("debug: " + debugInfo);
        }

        private void WriteLine(string message)
        {
            var time = DateTime.Now.ToString("HH:mm:ss.fff");
            var fullMessage = time + ": " + message;

            Console.WriteLine(fullMessage);
            LogAppended?.Invoke(this, fullMessage);
            
            ServerHandshake.SendWorkspaceLogMessage(fullMessage).ConfigureAwait(false);
        }
    }
}