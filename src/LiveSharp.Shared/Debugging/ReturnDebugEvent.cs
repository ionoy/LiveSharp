
#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Debugging
#else
namespace LiveSharp.Shared.Debugging
#endif
{
    public class ReturnDebugEvent : DebugEvent
    {
        public ReturnDebugEvent(int invocationId, object value)
        {
            InvocationId = invocationId;
            ReturnValue = value;
        }

        public ReturnDebugEvent()
        { }

        public object ReturnValue { get; set; }
    }
}