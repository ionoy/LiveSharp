using System;

namespace LiveSharp
{
    public interface ILiveSharpLogger
    {
        bool IsDebugLoggingEnabled { get; set; }
        
        void LogError(string errorMessage);
        void LogError(string errorMessage, Exception e);
        void LogMessage(string message);
        void LogWarning(string warning);
        void LogDebug(string debugInfo);
    }
}