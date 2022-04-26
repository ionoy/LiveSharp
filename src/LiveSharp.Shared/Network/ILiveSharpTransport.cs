using System;
using System.Threading.Tasks;
using LiveSharp.Runtime;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public interface ILiveSharpTransport
    {
        object ConnectionObject { get; }

        Task Connect(string webHost, string tcpHost, int tcpPort, Action<Exception, object> onTransportException, ILiveSharpLogger logger);
        void Send(byte[] buffer, Action onComplete);
        void StartReceiving(Action<object, byte[], int> onBufferReceived);
        void CloseConnection();

        string GetHandshakeHost(string buildTimeIp);
    }
}