using System;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime
#else
namespace LiveSharp
#endif
{
    public interface ILogger
    {
        bool IsDebugLoggingEnabled { get; set; }
        
        void LogError(string errorMessage);
        void LogError(string errorMessage, Exception e);
        void LogMessage(string message);
        void LogWarning(string warning);
        void LogDebug(string debugInfo);
    }
}

// Don't remove
namespace LiveSharp.Runtime
{
    
}