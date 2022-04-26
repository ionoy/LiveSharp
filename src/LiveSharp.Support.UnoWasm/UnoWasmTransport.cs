using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using LiveSharp.Runtime;
using LiveSharp.Runtime.Network;

namespace LiveSharp.Support.UnoWasm
{
    public class UnoWasmTransport : ILiveSharpTransport
    {
        private HubConnection _hubConnection;
        private Action<Exception, object> _onTransportException;
        public object ConnectionObject => _hubConnection;
        
        
        public async Task Connect(string webHost, string tcpHost, int tcpPort, Action<Exception, object> onTransportException, ILiveSharpLogger logger)
        {
            var uri = new Uri(webHost);
            var httpsPort = uri.Port;
            var requestUri = $"https://localhost.livesharp.net:{httpsPort}/livesharp";
            
            logger.LogMessage("Connecting to host " + requestUri);
            
            // First, we request the actual host endpoint from the host TcpClient 
            //var result = await httpClient.GetStringAsync(requestUri);

            _onTransportException = onTransportException;
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(requestUri)
                .Build();

            await _hubConnection.StartAsync();
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
            _hubConnection?.DisposeAsync();
        }

        public string GetHandshakeHost(string buildTimeIp)
        {
            return "https://localhost.livesharp.net";
        }
    }
}