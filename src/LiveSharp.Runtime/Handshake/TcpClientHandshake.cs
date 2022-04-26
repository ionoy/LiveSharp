using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSharp.Runtime.Handshake
{
    public class TcpClientHandshake : IHandshakeClient
    {
        private readonly TcpClient _client;
        public TcpClientHandshake()
        {
            _client = new TcpClient();
        }
        
        public async Task<HttpResponseMessage> PostAsync(string host, int port, string url, FormUrlEncodedContent formUrlEncodedContent, ILogger logger)
        {
            host = host.ToLower().Replace("http://", "").Replace("https://", "");
            
            var timeout = TimeSpan.FromSeconds(3);     
            var cancellationCompletionSource = new TaskCompletionSource<bool>();

            using var cts = new CancellationTokenSource(timeout);
            
            var connectTask = _client.ConnectAsync(host, port);

            using var register = cts.Token.Register(() => cancellationCompletionSource.TrySetResult(true));
            
            if (connectTask != await Task.WhenAny(connectTask, cancellationCompletionSource.Task))
                throw new OperationCanceledException(cts.Token);

            if (connectTask.Exception != null)
                throw connectTask.Exception;
            
            using var stream = _client.GetStream();
            using var reader = new StreamReader(stream);
            using var writer = new StreamWriter(stream) {
                AutoFlush = true
            };
            
            var payload = await formUrlEncodedContent.ReadAsStringAsync();
            
            await writer.WriteAsync("POST /connect/runtime HTTP/1.1\r\n");
            await writer.WriteAsync($"Host: {host}:{port}\r\n");
            await writer.WriteAsync("pragma: no-cache\r\n");
            await writer.WriteAsync("cache-control: no-cache\r\n");
            await writer.WriteAsync("user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/89.0.4389.114 Safari/537.36\r\n");
            await writer.WriteAsync("content-type: application/x-www-form-urlencoded\r\n");
            await writer.WriteAsync($"content-length: {payload.Length + 2}\r\n");
            await writer.WriteAsync("accept: */*\r\n\r\n");
            await writer.WriteAsync(payload + "\r\n");

            var readingAddress = false;
            var finishedReadingAddress = false;
            var serverAddressString = "";
            var response = "";
            var initialTimeout = 10000;
            var responseReadTimeout = 3000;
            
            while (true) {
                var readerTask = reader.ReadLineAsync();
                var timeoutTask = Task.Delay(response.Length > 0 ? responseReadTimeout : initialTimeout);
                
                var resultTask = await Task.WhenAny(readerTask, timeoutTask);

                if (resultTask == timeoutTask)
                    break;

                var line = readerTask.Result;
                
                if (line.Contains(@"<?xml version=""1.0"" encoding=""utf-16""?>"))
                    readingAddress = true;

                if (readingAddress)
                    serverAddressString += line;

                response += line + Environment.NewLine;

                if (line.Contains(@"</ServerAddress>")) {
                    finishedReadingAddress = true;
                    break;
                }
            }

            if (!finishedReadingAddress) {
                logger.LogError(response);
                
                return new HttpResponseMessage(HttpStatusCode.InternalServerError) {
                    Content = new StringContent(response)
                };
            }
            
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(serverAddressString)
            };
        }
        public void Dispose()
        {
            _client.Dispose();
        }
    }
}