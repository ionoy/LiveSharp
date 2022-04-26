using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LiveSharp.Runtime.Debugging;
using LiveSharp.Runtime.Infrastructure;

namespace LiveSharp.Runtime
{
    public static class LiveSharpDebugger
    {
        // this is a potential memory leak
        // currently we only remove instances when `ret` was encountered
        // we also need to handle `throw` and `jmp` somehow
        private static readonly ConcurrentDictionary<object, int> _closureInstances = new ConcurrentDictionary<object, int>();
        private static readonly ConcurrentDictionary<int, ImmutableStack<object>> _closureInstancesByInvocation = new ConcurrentDictionary<int, ImmutableStack<object>>();
        private static readonly ConcurrentQueue<DebugEvent> DebugEvents = new ConcurrentQueue<DebugEvent>();
        private static readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private static int _uniqueId;
        private static int GetUniqueId() => _uniqueId++;

        public static event Action<DebugEvent[]> EventsReady;
            
        public static void StartSending()
        {
            Task.Run(async () => {
                while (!_cts.IsCancellationRequested) {
                    try {
                        while (!_cts.IsCancellationRequested) {
                            var eventsReadyEvent = EventsReady;
                            if (eventsReadyEvent != null && DebugEvents.Count > 0) {
                                var eventsCount = DebugEvents.Count;
                                var eventArray = new DebugEvent[eventsCount];
                                
                                for (int i = 0; i < eventsCount; i++)
                                    if (DebugEvents.TryDequeue(out var evt))
                                        eventArray[i] = evt;

                                eventsReadyEvent(eventArray);
                            }

                            await Task.Delay(50);
                        }
                    } catch (Exception e) {
                        LiveSharpRuntime.Logger.LogError("Sending debugging events failed", e);
                    }

                    await Task.Delay(1000);
                }
            }, _cts.Token);
        }

        public static void Stop()
        {
            _cts.Cancel();
        }
        public static int Start(object instance, string methodIdentifier, string localNames, string parameterNames, bool hasReturnValue, params object[] arguments)
        {
            var invocationId = instance != null && _closureInstances.TryGetValue(instance, out var parentInvocationId) 
                ? parentInvocationId 
                : GetUniqueId();
            var startDebugEvent = new StartDebugEvent(methodIdentifier, invocationId, localNames, parameterNames, hasReturnValue, arguments);
            
            DebugEvents.Enqueue(startDebugEvent);
            
            //Debug.WriteLine(WebUtility.HtmlDecode(startDebugEvent.ToLine()));
            
            return invocationId;
        }
        public static void Assign(object value, int invocationId, string name)
        {
            var assignDebugEvent = new AssignDebugEventDebug(invocationId, name, value);
            DebugEvents.Enqueue(assignDebugEvent);
            
            //Debug.WriteLine(WebUtility.HtmlDecode(assignDebugEvent.ToLine()));
        }
        public static void Assign(object value, int invocationId, int slotIndex)
        {
            var assignDebugEvent = new AssignDebugEvent(invocationId, slotIndex, value);
            DebugEvents.Enqueue(assignDebugEvent);
            
            //Debug.WriteLine(WebUtility.HtmlDecode(assignDebugEvent.ToLine()));
        }
        public static void Return(int invocationId, object value)
        {
            var returnDebugEvent = new ReturnDebugEvent(invocationId, value);
            DebugEvents.Enqueue(returnDebugEvent);

            if (_closureInstancesByInvocation.TryRemove(invocationId, out var closureInstances)) {
                while (!closureInstances.IsEmpty) {
                    // FIX, need better suiting immutable list
                    var t = closureInstances.Pop();
                    closureInstances = t.Item1;
                    _closureInstances.TryRemove(t.Item2, out _);
                }
            }
            
            //Debug.WriteLine(WebUtility.HtmlDecode(returnDebugEvent.ToLine()));
        }
        public static void RegisterClosureInstance(object instance, int invocationId)
        {
            _closureInstances[instance] = invocationId;
            _closureInstancesByInvocation.AddOrUpdate(invocationId, _ => new ImmutableStack<object>(), (_, list) => list.Push(instance));
        }
    }
}