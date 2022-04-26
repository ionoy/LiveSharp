using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using LiveSharp.Interfaces;
using LiveSharp.Runtime.Infrastructure;

namespace LiveSharp.ServerClient
{
    public class LiveServerClient : IDisposable
    {
        public int CurrentPort { get; private set; }
        public bool PlatformNotSupported { get; private set; }
        
        private readonly string _host;
        private readonly ILogger _logger;
        private readonly Action<ServerMessage> _onMessageReceived;
        private readonly ConcurrentQueue<ServerMessage> _sendQueue = new ConcurrentQueue<ServerMessage>();
        private readonly MessageParser _parser = new MessageParser(null);
        private readonly SemaphoreSlim _reconnectSemaphor = new SemaphoreSlim(0, 1);
        private readonly CircularBuffer<int> _ports; 
        private readonly ILiveSharpTransport _transport;
        private volatile bool _isBusy = true;
        private bool _isReconnecting;
        private bool _isConnected;

        public LiveServerClient(string host, IReadOnlyList<int> portRange, ILogger logger, Action<ServerMessage> onMessageReceived, ILiveSharpTransport transport = null)
        {
            _transport = transport ?? new SocketTransport();
            _host = host;
            _ports = new CircularBuffer<int>(portRange);
            _logger = logger;
            _onMessageReceived = onMessageReceived;
            _parser.MessageParsed += MessageParsed;
            
            logger.LogMessage("Transport type: " + _transport);
        }
        
        public async Task Connect(Action onConnect)
        {
            var port = _ports.Get();

            while (true) {
                try {
                    var connection = _transport.Connect(_host, port, OnTransportException, _logger); 
                    var result = await Task.WhenAny(connection, Task.Delay(3000));

                    // if connection completed first
                    if (result == connection && connection.Exception == null) {
                        _logger.LogMessage("Connected to " + _host + ":" + port);

                        CurrentPort = port;
                        _transport.StartReceiving(OnBufferReceived);

                        ProcessSendQueue();

                        _isConnected = true;
                        
                        onConnect();

                        _isReconnecting = false;

                        await _reconnectSemaphor.WaitAsync();

                        _isConnected = false;
                        _isReconnecting = true;

                        port = _ports.Get();

                        _logger.LogMessage("Reconnecting to " + _host + ":" + port);
                    }
                    else {
                        port = _ports.Get();
                    }

                    if (connection.Exception != null) {
                        _logger.LogError("Connection exception: " + connection.Exception);
                    }

                    while (!_sendQueue.IsEmpty)
                        _sendQueue.TryDequeue(out var _);

                    _transport.CloseConnection();
                }
                catch (PlatformNotSupportedException) {
                    PlatformNotSupported = true;
                    _logger.LogMessage("Platform doesn't support Socket connection");
                    break;
                }
                catch (Exception e) {
                    _logger.LogWarning("Connection failed: " + e);
                    
                    await Task.Delay(1000);
                }
            }
        }

        private void OnBufferReceived(object connectionObject, byte[] buffer, int length)
        {
            if (length == 0)
            {
                TryReconnect(connectionObject);
                return;
            }
            
            //_logger.LogDebug("Buffer received: " + length);
            _parser.Feed(buffer, length);
        }

        public void SendBroadcast(string message, byte contentType, int groupId)
        {
            var buffer = Encoding.Unicode.GetBytes(message);
            SendContent(buffer, contentType, MessageType.Broadcast, groupId);
        }

        public void SendBroadcast(byte[] buffer, byte contentType, int groupId)
        {
            SendContent(buffer, contentType, MessageType.Broadcast, groupId);
        }

        public void SendBroadcast(IReadOnlyList<byte> buffer, byte contentType, int groupId)
        {
            SendContent(buffer, contentType, MessageType.Broadcast, groupId);
        }
        
        public void JoinGroup(int groupId)
        {
            SendContent(new byte[0], 0, MessageType.JoinGroup, groupId);
        }

        private void SendContent(byte[] buffer, byte contentType, MessageType messageType, int parameter)
        {
            Send(new ServerMessage(buffer, contentType, messageType, parameter));
        }

        private void Send(ServerMessage message)
        {
            if (!_isBusy && _isConnected) {
                _isBusy = true;
                Send(message.CreateBuffer());
            } else {
                _sendQueue.Enqueue(message);
            }
        }

        private void Send(byte[] buffer)
        {
            try {
                _transport.Send(buffer, ProcessSendQueue);
            }
            catch (Exception e) {
                _logger.LogWarning("Send failed: " + e);
            }
        }

        private void OnTransportException(Exception e, object connectionObject)
        {
            _logger.LogWarning("Transport failed: " + e);
            TryReconnect(connectionObject);
        }
        
        private void TryReconnect(object connectionObject)
        {
            while (!_sendQueue.IsEmpty)
                _sendQueue.TryDequeue(out var _);
            
            _isBusy = false;
            
            if (connectionObject != _transport.ConnectionObject)
                 return;
            
            _transport.CloseConnection();
            
            if (!_isReconnecting && _reconnectSemaphor.CurrentCount == 0)
                _reconnectSemaphor.Release();
        }

        private void ProcessSendQueue()
        {
            if (_sendQueue.TryDequeue(out var nextMessage)) {
                _transport.Send(nextMessage.CreateBuffer(), ProcessSendQueue);
            }
            else
                _isBusy = false;
        }

        private void MessageParsed(object sender, ParserEventArgs<ServerMessage> e)
        {
            _onMessageReceived(e.Message);
        }
        
        public void Dispose()
        {
            _transport.CloseConnection();
        }
    }
}