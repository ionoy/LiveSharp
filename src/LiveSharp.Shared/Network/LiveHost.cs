using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LiveSharp.ServerClient;
using LiveSharp.Runtime;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public class LiveHost
    {
        private readonly TcpListener _tcpListener;
        private readonly Action<ServerMessage, Client> _messageReceived;
        private readonly Action _clientDisconnected;
        private readonly ILogger _logger;
        private ImmutableList<Client> _clients = ImmutableList<Client>.Empty;

        public LiveHost(Action<ServerMessage, Client> messageReceived, Action clientDisconnected, ILogger logger)
        {
            _logger = logger;
            _messageReceived = messageReceived;
            _clientDisconnected = clientDisconnected;
            _tcpListener = new TcpListener(IPAddress.Any, 0);
        }

        public int Start()
        {
            _tcpListener.Start();
            _tcpListener.BeginAcceptSocket(AcceptClient, null);

            var port = ((IPEndPoint)_tcpListener.LocalEndpoint).Port;

            _logger.LogMessage("Started listening on port " + port);
            
            return port;
        }

        public void Stop()
        {
            _tcpListener.Stop();
        }

        public void SendBroadcast(byte[] buffer, byte contentType, int groupId)
        {
            var serverMessage = new ServerMessage(buffer, contentType, MessageType.Broadcast, groupId);

            SendBroadcast(serverMessage);
        }

        public void SendBroadcast(ServerMessage message)
        {
            var messageBuffer = message.CreateBuffer();

            foreach (var client in _clients) client.Send(messageBuffer);

            var disconnectedClients = _clients
                .Where(c => !c.IsConnected)
                .ToArray();

            foreach (var disconnectedClient in disconnectedClients) {
                _clients = _clients.Remove(disconnectedClient);
            }
        }

        private void AcceptClient(IAsyncResult ar)
        {
            try {
                _tcpListener.BeginAcceptSocket(AcceptClient, null);

                var socket = _tcpListener.EndAcceptSocket(ar);
                var client = new Client(socket, _logger);

                _logger.LogMessage("client connected: " + socket.RemoteEndPoint);

                var buffer = new byte[8192];
                var parser = new MessageParser(client);

                parser.MessageParsed += Parser_MessageParsed;

                _clients = _clients.Add(client);

                StartReceiving(client, parser, buffer);
            } catch (Exception e) {
                _logger.LogError("AAAAAAAAAAAA", e);
            }
        }

        private void Parser_MessageParsed(object sender, ParserEventArgs<ServerMessage> args)
        {
            _messageReceived(args.Message, (Client)args.Payload);
        }

        private void StartReceiving(Client client, MessageParser parser, byte[] buffer)
        {
            try {
                client.Socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, EndReceive, new State(client, parser, buffer));
            } catch (Exception e) {
                _logger.LogError("BeginReceive failed", e);

                try {
                    _tcpListener.BeginAcceptSocket(AcceptClient, null);
                } catch (Exception e2) {
                    _logger.LogError("BeginAcceptSocket failed", e2);
                }
            }
        }

        private void EndReceive(IAsyncResult ar)
        {
            var state = (State)ar.AsyncState;

            try {
                var bytesRead = state.Client.Socket.EndReceive(ar);

                //Console.WriteLine("message received (" + bytesRead + ")");

                if (bytesRead == 0) {
                    DisconnectClient(state);
                    return;
                }

                state.Parser.Feed(state.Buffer, bytesRead);

                StartReceiving(state.Client, state.Parser, state.Buffer);
            } catch (Exception ex) {
                _logger.LogError("EndReceive failed", ex);
                DisconnectClient(state);
            }
        }

        private void DisconnectClient(State state)
        {
            state.Parser.MessageParsed -= Parser_MessageParsed;
            _logger.LogMessage("client disconnected: " + state.Client.Socket.RemoteEndPoint);
            _clientDisconnected?.Invoke();
        }

        public int GetAssignedPort()
        {
            return ((IPEndPoint)_tcpListener.LocalEndpoint).Port;
        }

        struct State
        {
            public Client Client;
            public MessageParser Parser;
            public byte[] Buffer;

            public State(Client client, MessageParser parser, byte[] buffer)
            {
                Client = client;
                Parser = parser;
                Buffer = buffer;
            }
        }
    }
}