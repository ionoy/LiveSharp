using System;
using System.Threading.Tasks;
#if NETSTANDARD2_1 || NET5_0_OR_GREATER

using LiveSharp.Runtime.Network;
using Microsoft.AspNetCore.SignalR.Client;

namespace LiveSharp.Support.Blazor
{
    public class BlazorTransport : ILiveSharpTransport
    {
        private HubConnection _hubConnection;
        private Action<Exception, object> _onTransportException;
        private ILiveSharpLogger _logger;

        public object ConnectionObject => _hubConnection;

        public async Task Connect(string webHost, string tcpHost, int tcpPort, Action<Exception, object> onTransportException, ILiveSharpLogger logger)
        {
            _logger = logger;

            var uri = new Uri(webHost);
            var httpsPort = uri.Port;
            var requestUri = $"https://localhost.livesharp.net:{httpsPort}/livesharp";
            
            logger.LogMessage("Connecting to host " + requestUri);
            
            // First, we request the actual host endpoint from the host TcpClient 
            //var result = await httpClient.GetStringAsync(requestUri);

            _onTransportException = onTransportException;
            _hubConnection = new HubConnectionBuilder()
                .WithAutomaticReconnect()
                .WithUrl(requestUri)
                .Build();

            _hubConnection.Closed += OnHubConnectionOnClosed;
            _hubConnection.Reconnecting += HubConnectionOnReconnecting;
            _hubConnection.Reconnected += HubConnectionOnReconnected;
            
            await _hubConnection.StartAsync();
        }

        private Task HubConnectionOnReconnected(string arg)
        {
            // _logger.LogMessage("LiveSharp reconnected");
            return Task.FromResult(true);
        }

        private Task HubConnectionOnReconnecting(Exception arg)
        {
            // _logger.LogMessage("LiveSharp reconnecting");
            return Task.FromResult(true);
        }

        private Task OnHubConnectionOnClosed(Exception exception)
        {
            _logger.LogError($"LiveSharp connection closed: {exception ?? new Exception("Unknown reason")}");
            return Task.FromResult(true);
        }

        public void Send(byte[] buffer, Action onComplete)
        {
            _hubConnection.SendAsync("ServerMessage", buffer).ContinueWith(t =>
            {
                if (t.Exception != null) {
                    _onTransportException(t.Exception, _hubConnection);
                } else {
                    onComplete();
                }
            });
        }

        public void StartReceiving(Action<object, byte[], int> onBufferReceived)
        {
            _hubConnection.On<byte[]>("ServerMessage", (buffer) =>
            {
                onBufferReceived(_hubConnection, buffer, buffer.Length);
            });
        }

        public void CloseConnection()
        {
            // _logger?.LogMessage("BlazorTransport disconnecting");
            // try {
            //     _hubConnection?.DisposeAsync();
            // }
            // catch (MissingMethodException) {
            //     // ignore missing method for signalr 5.0 
            // }
        }

        public string GetHandshakeHost(string buildTimeIp)
        {
            return "https://localhost.livesharp.net";
        }
    }
}
#endif