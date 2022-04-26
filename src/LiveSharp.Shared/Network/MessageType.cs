#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public enum MessageType : byte
    {
        JoinGroup = 1,
        Broadcast
    }
}
