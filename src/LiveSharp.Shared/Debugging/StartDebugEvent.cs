using System.Linq;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Debugging
#else
namespace LiveSharp.Shared.Debugging
#endif
{
    public class StartDebugEvent : DebugEvent
    {
        public StartDebugEvent() 
        {}
        public StartDebugEvent(string methodIdentifier, int invocationId, string localNames, string parameterNames, bool hasReturnValue, object[] arguments)
        {
            MethodIdentifier = methodIdentifier;
            InvocationId = invocationId;
            LocalNames = localNames;
            ParameterNames = parameterNames;
            Arguments = arguments;
            HasReturnValue = hasReturnValue;
        }

        public string MethodIdentifier { get; set; }
        public string LocalNames { get; set; }
        public string ParameterNames { get; set; }
        public bool HasReturnValue { get; set; }
        public object[] Arguments { get; set; }

        public override int GetHashCode()
        {
            return InvocationId;
        }
    }
}