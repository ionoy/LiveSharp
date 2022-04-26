using System;
using System.Collections.Concurrent;
using System.Linq;
using LiveSharp.Runtime;
using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    // public class LiveServer
    // {
    //     public int ServerPort { get; } 
    //     
    //     private readonly LiveHost _host;
    //     private readonly ConcurrentBag<Action<ServerMessage, INetworkClient>> _handlers = new();
    //     private readonly Action<INetworkClient, int> _clientJoinedGroup;
    //     private readonly Action<INetworkClient, int> _clientLeftGroup;
    //     private readonly ILogger _logger;
    //     private readonly ConcurrentDictionary<int, GroupList> _groups = new();
    //     
    //     public LiveServer(int serverPort, Action<INetworkClient, int> clientJoinedGroup,
    //         Action<INetworkClient, int> clientLeftGroup, ILogger logger)
    //     {
    //         ServerPort = serverPort;
    //         
    //         _clientJoinedGroup = clientJoinedGroup;
    //         _clientLeftGroup = clientLeftGroup;
    //         _logger = logger;
    //
    //         try {
    //             _logger.LogMessage($"Initializing LiveHost at {serverPort}");
    //             _host = new LiveHost(serverPort, MessageReceived, ClientDisconnected, ClientConnected, _logger);
    //         } catch (Exception e) {
    //             _logger.LogError("Initializing LiveHost failed: ", e);
    //             throw;
    //         }
    //     }
    //
    //     public void RegisterMessageHandler(Action<ServerMessage, INetworkClient> messageHandler)
    //     {
    //         _handlers.Add(messageHandler);
    //     }
    //
    //     public void SendBroadcast(byte[] buffer, byte contentType, int groupId)
    //     {
    //         var serverMessage = new ServerMessage(buffer, contentType, MessageType.Broadcast, groupId);
    //
    //         SendBroadcast(serverMessage, new INetworkClient[0]);
    //     }
    //
    //     public void SendBroadcast(ServerMessage message, IReadOnlyList<INetworkClient> clients)
    //     {
    //         clients ??= new INetworkClient[0];
    //         var groupId = message.Parameter;
    //         var messageBuffer = message.CreateBuffer();
    //         var groupList = _groups.GetOrAdd(groupId, _ => new GroupList());
    //
    //         foreach (var client in groupList.Clients.Where(c => clients.Contains(c)))
    //         {
    //             //Console.WriteLine("Sending broadcast to " + client.Socket.RemoteEndPoint);                
    //             client.Send(messageBuffer);
    //         }
    //
    //         var disconnectedClients = groupList.Clients
    //                                            .Where(c => !c.IsConnected)
    //                                            .ToArray();
    //
    //         foreach (var disconnectedClient in disconnectedClients) {
    //             groupList.Remove(disconnectedClient);
    //             RemoveClientFromGroups(disconnectedClient);
    //         }
    //     }
    //
    //     private void ClientConnected(INetworkClient client)
    //     {
    //         
    //     }
    //
    //     private void ClientDisconnected(INetworkClient client)
    //     {
    //         RemoveClientFromGroups(client);
    //     }
    //
    //     private void RemoveClientFromGroups(INetworkClient client)
    //     {
    //         foreach (var kvp in _groups) {
    //             var groupId = kvp.Key;
    //             var groupList = kvp.Value;
    //             var clientWasInGroup = groupList.Clients.Contains(client);
    //             
    //             groupList.Remove(client);
    //             
    //             if (clientWasInGroup) 
    //                 _clientLeftGroup?.Invoke(client, groupId);
    //         }
    //     }
    //
    //     public void MessageReceived(ServerMessage message, INetworkClient client)
    //     {
    //         foreach (var handler in _handlers)
    //             handler(message, client);
    //         
    //         if (message.MessageType == MessageType.JoinGroup) {
    //             var groupId = message.Parameter;
    //             var groupList = _groups.GetOrAdd(groupId, _ => new GroupList());
    //
    //             _logger.LogMessage("Client joined group " + groupId);
    //
    //             groupList.Add(client);
    //             
    //             _clientJoinedGroup?.Invoke(client, groupId);
    //         }
    //     }
    //
    //     public int GetAssignedPort() => _host.GetAssignedPort();
    // }
}
