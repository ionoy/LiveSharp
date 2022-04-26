using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSharp.Runtime.Handshake
{
    public class HttpClientHandshake : IHandshakeClient
    {
        private readonly HttpClient _client;
        public HttpClientHandshake()
        {
            _client = new HttpClient();
        }
        
        public Task<HttpResponseMessage> PostAsync(string host, int port, string url, FormUrlEncodedContent formUrlEncodedContent, ILogger logger)
        {
            using var cts = new CancellationTokenSource(3000);
            return _client.PostAsync(url, formUrlEncodedContent, cts.Token);
        }
        
        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}