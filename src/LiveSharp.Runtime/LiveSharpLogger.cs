using LiveSharp.Shared;
using System;

namespace LiveSharp.Runtime
{
    public class LiveSharpLogger : ILiveSharpLogger
    {
        public bool IsDebugLoggingEnabled { get; set; }
        
        private readonly ILogger _loggerImpl;

        public LiveSharpLogger(ILogger logger)
        {
            _loggerImpl = logger;
        }
        public void LogError(string errorMessage)
        {
            _loggerImpl.LogError(errorMessage);
        }

        public void LogError(string errorMessage, Exception e)
        {
            _loggerImpl.LogError(errorMessage, e);
        }

        public void LogMessage(string message)
        {
            _loggerImpl.LogMessage(message);
        }

        public void LogWarning(string warning)
        {
            _loggerImpl.LogWarning(warning);
        }

        public void LogDebug(string debugInfo)
        {
            _loggerImpl.LogDebug(debugInfo);
        }
    }
}