using LiveSharp.Shared.Network;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LiveSharp.Dashboard
{
    public class BlazorHubClient : INetworkClient
    {
        private readonly IHubContext<BlazorHub> _blazorHubContext;

        public BlazorHubClient(IHubContext<BlazorHub> blazorHubContext)
        {
            _blazorHubContext = blazorHubContext;
        }

        public void Send(byte[] buffer)
        {
            _blazorHubContext.Clients.All.SendAsync(nameof(ServerMessage), buffer);
        }

        public bool IsConnected => true;
    }
}