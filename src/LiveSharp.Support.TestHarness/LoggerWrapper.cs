using System;

namespace LiveSharp.Support.TestHarness
{
    public class LoggerWrapper : ILogger
    {
        private readonly ILiveSharpLogger _runtimeLogger;

        public LoggerWrapper(ILiveSharpLogger runtimeLogger)
        {
            _runtimeLogger = runtimeLogger;
        }

        public bool IsDebugLoggingEnabled { get; set; }
        public void LogError(string errorMessage) => _runtimeLogger.LogError(errorMessage);

        public void LogError(string errorMessage, Exception e) => _runtimeLogger.LogError(errorMessage, e);

        public void LogMessage(string message) => _runtimeLogger.LogMessage(message);

        public void LogWarning(string warning) => _runtimeLogger.LogWarning(warning);

        public void LogDebug(string debugInfo) => _runtimeLogger.LogDebug(debugInfo);
    }
}