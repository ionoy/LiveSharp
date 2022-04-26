#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public interface INetworkClient
    {
        void Send(byte[] buffer);
        bool IsConnected { get; }
    }
}