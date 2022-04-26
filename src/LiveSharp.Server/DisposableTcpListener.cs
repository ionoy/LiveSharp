using System;
using System.Net;
using System.Net.Sockets;

namespace LiveSharp.Server
{
    class DisposableTcpListener : TcpListener, IDisposable
    {
        public DisposableTcpListener(int port) : base(IPAddress.Any, port)
        {
        }

        public DisposableTcpListener(IPAddress localaddr, int port) : base(localaddr, port)
        {
        }

        public DisposableTcpListener(IPEndPoint localEP) : base(localEP)
        {
        }

        public void Dispose()
        {
            if (Active)
                Stop();
        }
    }
}