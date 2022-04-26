
#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Debugging
#else
namespace LiveSharp.Shared.Debugging
#endif
{
    public class AssignDebugEvent : DebugEvent
    {
        public int SlotIndex { get; set; }
        public object Value { get; set; }
        
        public AssignDebugEvent(int invocationId, int slotIndex, object value)
        {
            InvocationId = invocationId;
            SlotIndex = slotIndex;
            Value = value;
        }

        public AssignDebugEvent()
        { }
    }
    
    class AssignDebugEventDebug : DebugEvent
    {
        public string Name { get; }
        public object Value { get; }
        
        public AssignDebugEventDebug(int invocationId, string name, object value)
        {
            InvocationId = invocationId;
            Name = name;
            Value = value;
        }
    }
}