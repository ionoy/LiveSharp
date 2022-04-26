using System;

namespace LiveSharp.Rewriters
{
    public class RewriteLogger
    {
        private readonly Action<string> _logMessage;
        private readonly Action<string> _logWarning;
        private readonly Action<string> _logError;
        private readonly Action<string, Exception> _logErrorEx;

        public RewriteLogger(
            Action<string> logMessage, 
            Action<string> logWarning, 
            Action<string> logError,
            Action<string, Exception> logErrorEx)
        {
            _logMessage = logMessage ?? throw new ArgumentNullException(nameof(logMessage));
            _logWarning = logWarning ?? throw new ArgumentNullException(nameof(logWarning));
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
            _logErrorEx = logErrorEx ?? throw new ArgumentNullException(nameof(logErrorEx));
        }

        public void LogMessage(string message) => _logMessage(message);
        public void LogWarning(string warning) => _logWarning(warning);
        public void LogError(string error) => _logError(error);
        public void LogErrorEx(string error, Exception e) => _logErrorEx(error, e);
    }
}