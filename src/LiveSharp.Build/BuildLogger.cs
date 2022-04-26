using System;

namespace LiveSharp.Build
{
    class BuildLogger
    {
        private readonly BuildTaskLogger _inner;
        public bool IsDebugLoggingEnabled { get; set; }

        public BuildLogger(BuildTaskLogger inner)
        {
            _inner = inner;
        }

        public void LogError(string errorMessage)
        {
            _inner.LogError(errorMessage);
        }

        public void LogError(string errorMessage, Exception e)
        {
            _inner.LogError(errorMessage + Environment.NewLine + e);
        }

        public void LogMessage(string message)
        {
            _inner.LogMessage(message);
        }

        public void LogWarning(string warning)
        {
            _inner.LogWarning(warning);
        }

        public void LogDebug(string debugInfo)
        {
            _inner.LogMessage(debugInfo);
        }
    }
}