#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Api
#else
namespace LiveSharp.Shared.Api
#endif
{
    public class ServerAddress : XmlMessage<ServerAddress>
    {
        public string Url { get; set; }
        public int TcpPort { get; set; }

        public override string ToString()
        {
            return $"Web host: {Url} Tcp port: {TcpPort}";
        }
    }
}