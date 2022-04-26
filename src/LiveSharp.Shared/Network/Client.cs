using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using LiveSharp.Runtime;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public class Client : INetworkClient
    {
        public bool IsConnected => _socket.Connected;
        public Socket Socket => _socket;

        private readonly ILogger _logger;
        private readonly Socket _socket;
        private Task _sendQueue = Task.FromResult(true);

        public Client(Socket socket, ILogger logger)
        {
            _logger = logger;
            _socket = socket;
        }

        public void Send(byte[] buffer)
        {
            _sendQueue = _sendQueue.ContinueWith(_ => {
                try {
                    _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, SendCallback, null);
                } catch (Exception e) {
                    _logger.LogError("Failed to BeginSend", e);
                }
            });
        }

        private void SendCallback(IAsyncResult ar)
        {
            try {
                _socket.EndSend(ar);
            } catch (Exception e) {
                _logger.LogError("Failed to EndSend", e);
            }
        }
    }
}
