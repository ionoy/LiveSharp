using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using LiveSharp.Interfaces;

namespace LiveSharp.ServerClient
{
    public class SocketTransport : ILiveSharpTransport
    {
        private Socket _socket;
        private readonly byte[] _buffer = new byte[4092];
        private Action<object, byte[], int> _onBufferReceived;
        private Action<Exception, object> _onTransportException;

        public object ConnectionObject => _socket;

        public Task Connect(string host, int port, Action<Exception, object> onTransportException, ILogger logger)
        {
            logger.LogMessage("Connecting to " + host + ":" + port);
            
            _onTransportException = onTransportException;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            return Task.Factory.FromAsync(_socket.BeginConnect, _socket.EndConnect, host, port, _socket);
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
    }
}