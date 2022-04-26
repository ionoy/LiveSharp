using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LiveSharp.Support.Blazor
{
    public class WeakReferenceList<TKey, TValue> where TValue : class
    {
        readonly Dictionary<TKey, LinkedList<WeakReference<TValue>>> _inner =
            new Dictionary<TKey, LinkedList<WeakReference<TValue>>>();

        readonly Dictionary<TKey, int> _cleanupCounters = new Dictionary<TKey, int>();
        readonly ConditionalWeakTable<TValue, object> _values = new ConditionalWeakTable<TValue, object>();
        readonly object _lock = new object();
        const int CleanupThreshold = 400;

        public void Add(TKey key, TValue value)
        {
            lock (_lock) {
                if (_inner.TryGetValue(key, out var linkedList)) {
                    // Make sure we don't add same instance twice because of base constructors
                    if (_values.TryGetValue(value, out _))
                        return;

                    _values.Add(value, null);

                    linkedList.AddLast(new WeakReference<TValue>(value));

                    var cleanedCount = Cleanup(linkedList, cleanHeadOnly: true);
                    if (cleanedCount > 0)
                        return;

                    var counterValue = _cleanupCounters[key]--;
                    if (counterValue <= 0) {
                        _cleanupCounters[key] = CleanupThreshold;
                        Cleanup(linkedList);
                    }
                }
                else {
                    var list = new LinkedList<WeakReference<TValue>>();
                    list.AddLast(new WeakReference<TValue>(value));
                    _inner[key] = list;
                    _cleanupCounters[key] = CleanupThreshold;
                }
            }
        }

        public IReadOnlyList<TValue> Get(TKey key)
        {
            lock (_lock) {
                if (_inner.TryGetValue(key, out var list)) {
                    var aliveValues = new List<TValue>();
                    for (var node = list.First; node != null;) {
                        var next = node.Next;

                        if (!node.Value.TryGetTarget(out var value))
                            list.Remove(node);
                        else
                            aliveValues.Add(value);

                        node = next;
                    }

                    return aliveValues;
                }
                else {
                    return new TValue[0];
                }
            }
        }
        
        public void Remove(TKey key, TValue component)
        {
            lock (_lock) {
                if (_inner.TryGetValue(key, out var linkedList)) {
                    var weakReference = linkedList.FirstOrDefault(r => r.TryGetTarget(out var c) && c == component);
                    if (weakReference != null) {
                        linkedList.Remove(weakReference);
                    }
                }

                _values.Remove(component);
            }
        }

        private int Cleanup(LinkedList<WeakReference<TValue>> linkedList, bool cleanHeadOnly = false)
        {
            var cleanedCount = 0;

            for (var node = linkedList.First; node != null;) {
                var next = node.Next;

                if (!node.Value.TryGetTarget(out _)) {
                    linkedList.Remove(node);
                    cleanedCount++;
                }
                else if (cleanHeadOnly)
                    return cleanedCount;

                node = next;
            }

            return cleanedCount;
        }
    }
}