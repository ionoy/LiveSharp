using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace LiveSharp.Runtime.Handshake
{
    internal class TestHarnessHandshake : IHandshakeClient
    {
        public Task<HttpResponseMessage> PostAsync(string host, int port, string url, FormUrlEncodedContent formUrlEncodedContent, ILogger logger)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("test harness")
            });
        }
        
        public void Dispose()
        {
            
        }
    }
}