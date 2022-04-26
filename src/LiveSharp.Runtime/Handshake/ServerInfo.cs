using LiveSharp.Runtime.Api;

namespace LiveSharp.Runtime.Handshake
{
    public abstract record ServerInfo
    {
        public record Found(ServerAddress ServerAddress) : ServerInfo;
        public record Missing(string error = "") : ServerInfo;
    }
}
    
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}