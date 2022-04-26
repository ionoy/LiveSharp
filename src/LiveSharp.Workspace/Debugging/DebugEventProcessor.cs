using LiveSharp.Shared.Debugging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiveSharp.Debugging
{
    public class DebugEventProcessor
    {
        private ConcurrentDictionary<int, List<DebugEvent>> _invocations = new();
        private ConcurrentDictionary<int, StartDebugEvent> _invocationInfos = new();
        private ConcurrentDictionary<int, string> _invocationMethodNames = new();
        private ConcurrentDictionary<string, List<int>> _invocationsByMethodNames = new();
        
        public event Action<StartDebugEvent> InvocationStarted;
        public event Action<DebugEvent> DebugEventAdded;
        
        public void FeedEvents(IReadOnlyList<DebugEvent> debugEvents)
        {
            foreach (var debugEvent in debugEvents) {
                if (debugEvent is StartDebugEvent sde) {
                    _invocations[sde.InvocationId] = new List<DebugEvent>();
                    _invocationInfos[sde.InvocationId] = sde;
                    _invocationMethodNames[sde.InvocationId] = sde.MethodIdentifier;
                    
                    InvocationStarted?.Invoke(sde);
                }
                
                if (_invocations.TryGetValue(debugEvent.InvocationId, out var eventList)) {
                    eventList.Add(debugEvent);
                    DebugEventAdded?.Invoke(debugEvent);
                }
            }
        }

        public ICollection<string> GetMethodsWithEvents()
        {
            return _invocationsByMethodNames.Keys;
        }

        public IReadOnlyList<int> GetInvocations(string methodName)
        {
            if (_invocationsByMethodNames.TryGetValue(methodName, out var invocationList))
                return invocationList;
            
            throw new InvalidOperationException($"Invocations not found for method {methodName}");
        }

        public IReadOnlyList<DebugEvent> GetDebugEvents(int invocationId)
        {
            if (_invocations.TryGetValue(invocationId, out var eventList))
                return eventList;
            
            throw new InvalidOperationException($"Debug Events not found for invocation {invocationId}");
        }
    }
}