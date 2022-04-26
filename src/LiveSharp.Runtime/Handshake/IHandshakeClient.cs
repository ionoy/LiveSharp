using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace LiveSharp.Runtime.Handshake
{
    public interface IHandshakeClient : IDisposable
    {
        Task<HttpResponseMessage> PostAsync(string host, int port, string url, FormUrlEncodedContent formUrlEncodedContent, ILogger logger);
    }
}