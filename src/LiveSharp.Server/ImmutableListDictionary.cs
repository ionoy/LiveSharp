// using LiveSharp.Shared.Network;
// using System.Collections.Concurrent;
// using System.Collections.Generic;
// using System.Collections.Immutable;
// using System.Linq;
//
// namespace LiveSharp.Server
// {
//     class ImmutableListDictionary<TKey, TListItem>
//     {
//         private readonly ConcurrentDictionary<TKey, ImmutableList<TListItem>> _storage = new();
//
//         public void Add(TKey key, TListItem item)
//         {
//             _storage.AddOrUpdate(key, ImmutableList.Create(item), (_, list) => list.Add(item));
//         }
//
//         public bool TryGetKey(TListItem item, out TKey key)
//         {
//             foreach (var kvp in _storage) {
//                 foreach (var listItem in kvp.Value) {
//                     if (EqualityComparer<TListItem>.Default.Equals(listItem, item)) {
//                         key = kvp.Key;
//                         return true;
//                     }
//                 }
//             }
//
//             key = default;
//
//             return false;
//         }
//
//         public bool TryGetList(TKey key, out ImmutableList<TListItem> list) => _storage.TryGetValue(key, out list);
//         
//         public void UpdateList(TKey key, ImmutableList<TListItem> valueList)
//         {
//             _storage[key] = valueList;
//         }
//     }
// }