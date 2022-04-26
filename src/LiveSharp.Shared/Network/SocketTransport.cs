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
    public class SocketTransport : ILiveSharpTransport
    {
        private Socket _socket;
        private readonly byte[] _buffer = new byte[4092];
        private Action<object, byte[], int> _onBufferReceived;
        private Action<Exception, object> _onTransportException;

        public object ConnectionObject => _socket;

        public Task Connect(string webHost, string tcpHost, int port, Action<Exception, object> onTransportException, ILiveSharpLogger logger)
        {
            logger.LogMessage("Connecting to " + tcpHost + ":" + port);
            
            _onTransportException = onTransportException;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            
            return Task.Factory.FromAsync(_socket.BeginConnect, _socket.EndConnect, tcpHost, port, _socket);
        }

        public void StartReceiving(Action<object, byte[], int> onBufferReceived)
        {
            _onBufferReceived = onBufferReceived ?? throw new ArgumentNullException(nameof(onBufferReceived));
            _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, EndReceive, _socket);
        }

        private void EndReceive(IAsyncResult ar)
        {
            try {
                var bytesRead = _socket.EndReceive(ar);

                if (bytesRead > 0) {
                    _onBufferReceived(_socket, _buffer, bytesRead);
                    _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, EndReceive, _socket);
                }
            }
            catch (Exception e) {
                _onTransportException(e, _socket);
            }
        }

        public void Send(byte[] buffer, Action onComplete)
        {
            AsyncCallback callback = ar =>
            {
                try {
                    _socket.EndSend(ar);
                    onComplete();
                }
                catch (Exception e) {
                    _onTransportException(e, _socket);
                }
            };
            
            _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, callback, _socket);
        }


        public void CloseConnection()
        {
            _socket?.Dispose();
        }

        public string GetHandshakeHost(string buildTimeIp)
        {
            return "http://" + buildTimeIp;
        }
    }
}