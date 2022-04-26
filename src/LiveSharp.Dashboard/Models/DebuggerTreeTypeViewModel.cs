using LiveSharp.Shared.Debugging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace LiveSharp.Dashboard.Models
{
    public class DebuggerTreeTypeViewModel
    {
        public string TypeName { get; }

        public ConcurrentDictionary<string, DebuggerTreeMethodViewModel> Methods { get; } = new();

        public DebuggerTreeTypeViewModel(string typeName)
        {
            TypeName = typeName;
        }
    }

    public class DebuggerTreeMethodViewModel
    {
        public string MethodIdentifier { get; }
        public string MethodName { get; }
        public DebuggerTreeTypeViewModel Parent { get; }
        public ConcurrentDictionary<int, DebuggerTreeInvocationViewModel> Invocations { get; } = new();

        public DebuggerTreeMethodViewModel(string methodIdentifier, string methodName, DebuggerTreeTypeViewModel parent)
        {
            MethodIdentifier = methodIdentifier;
            MethodName = methodName;
            Parent = parent;
        }
    }

    public class DebuggerTreeInvocationViewModel
    {
        public StartDebugEvent StartEvent { get; }
        public string[] VariableNames { get; }
        public ImmutableList<DebugEvent> DebugEvents { get; private set; } = ImmutableList<DebugEvent>.Empty;

        public DebuggerTreeInvocationViewModel(StartDebugEvent startEvent)
        {
            StartEvent = startEvent;
            VariableNames = (startEvent.ParameterNames + "," + startEvent.LocalNames).Split(",");
        }

        public void AddDebugEvent(DebugEvent debugEvent)
        {
            DebugEvents = DebugEvents.Add(debugEvent);
        }
        
        public string GetVariableName(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= VariableNames.Length) {
                return "#invalid slot index";
            }

            return VariableNames[slotIndex];
        }
    }
}