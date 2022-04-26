using System;
using LiveSharp.ServerClient;
using LiveSharp.Shared.Network;
using Microsoft.AspNetCore.SignalR;

namespace LiveSharp.Dashboard
{
    public class BlazorHub : Hub
    {
        // Single static Parser for all Hub instances
        public static MessageParser Parser = new(null);

        public BlazorHub()
        {
        }

        public void ServerMessage(byte[] buffer)
        {
            Parser.Feed(buffer, buffer.Length);
        }
    }
}