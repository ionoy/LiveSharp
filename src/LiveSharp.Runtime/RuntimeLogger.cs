using System;
using System.Diagnostics;
using System.Reflection;
using LiveSharp.Runtime.Network;

namespace LiveSharp.Runtime
{
    public class RuntimeLogger : ILogger
    {
        private LiveServerClient _client;

        public bool UseConsoleWriteLine {
            get;
            set;
        }

        public bool IsDebugLoggingEnabled { get; set; } = false;
        public bool IsRuntimeLoggerWriteLine { get; set; }

        public void LogError(string errorMessage)
        {
            Write("error: " + errorMessage);
        }

        public void LogError(string errorMessage, Exception e)
        {
            LogError(errorMessage + Environment.NewLine + UnwrapException(e));
        }

        public void LogMessage(string message)
        {
            Write(message);
        }

        public void LogWarning(string warning)
        {
            Write("warning: " + warning);
        }

        public void LogDebug(string debugInfo)
        {
            if (IsDebugLoggingEnabled)
                Write("debug: " + debugInfo);
        }

        private void Write(string text)
        {
            IsRuntimeLoggerWriteLine = true;
            
            if (UseConsoleWriteLine)
                Console.WriteLine("livesharp: " + text);
            else
                Debug.WriteLine("livesharp: " + text);
            
            IsRuntimeLoggerWriteLine = false;
            BroadcastRuntimeLog(text);
        }

        public void BroadcastRuntimeLog(string text)
        {
            try {
                _client?.SendBroadcast(text, ContentTypes.General.RuntimeLog, (int) BroadcastGroups.General);
            }
            catch (Exception e) {
                Write("Error sending log to client. " + Environment.NewLine + e);
            }
        }

        internal void SetServerClient(LiveServerClient client)
        {
            _client = client;
        }
        

        private Exception UnwrapException(Exception ex)
        {
            while (ex is TargetInvocationException tie)
                ex = tie.InnerException;
            return ex;
        }

    }
}
