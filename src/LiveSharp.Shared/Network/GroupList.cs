using System.Collections.Concurrent;
using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{

    public class GroupList
    {
        private readonly ConcurrentDictionary<INetworkClient, object> _storage = new ConcurrentDictionary<INetworkClient, object>();

        public ICollection<INetworkClient> Clients => _storage.Keys;

        public void Add(INetworkClient client)
        {
            _storage.TryAdd(client, null);
        }

        public void Remove(INetworkClient client)
        {
            _storage.TryRemove(client, out var _);
        }
    }
}
