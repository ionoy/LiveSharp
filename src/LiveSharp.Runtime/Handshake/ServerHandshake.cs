using LiveSharp.Runtime.Api;
using LiveSharp.Runtime.Network;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace LiveSharp.Runtime.Handshake
{
    public static class ServerHandshake
    {
        static readonly int[] ServerPortRange = {50540, 30540, 40540};
        public static async Task<ServerInfo> ConnectToServer(string handshakeHost, ProjectInfo projectInfo, ILiveSharpTransport transport, ILogger logger)
        {
            try {
                // We need a custom HTTP client for mobile clients 
                // because they don't allow non-HTTPS requests (at least with no hassle)
                // and we can't provide a certificate for a raw IP host
                using var client = GetHandshake(transport);

                var projectInfoXml = projectInfo.Serialize();

                for (int i = 0; i < 3; i++) {
                    foreach (var port in ServerPortRange) {
                        var hostPort = port;
                        if (handshakeHost.StartsWith("https"))
                            hostPort = hostPort - 1;
                        
                        var url = $"{handshakeHost}:{hostPort}/connect/runtime";
                        try {
                            logger.LogMessage($"Initiating handshake with {url}");
                        
                            var formUrlEncodedContent = new FormUrlEncodedContent(new[] {
                                new KeyValuePair<string, string>("projectInfo", projectInfoXml)
                            });
                        
                            var result = await client.PostAsync(handshakeHost, hostPort, url, formUrlEncodedContent, logger);
                        
                            if (result.IsSuccessStatusCode) {
                                var serverAddressXml = await result.Content.ReadAsStringAsync();
                                var serverAddress = ServerAddress.Deserialize(serverAddressXml);
                                
                                logger.LogMessage($"Received server address {serverAddress}");
                                
                                return new ServerInfo.Found(serverAddress);
                            }

                            return new ServerInfo.Missing(result.ReasonPhrase);
                        } catch (Exception e) {
                            logger.LogError($"Unable to connect to {url}", e);
                        }
                    }
                }

                throw new Exception("LiveSharp Server not found at ports 50540, 30540, 40540");
            } catch (Exception e) {
                logger.LogError("Connecting to LiveSharp server failed", e);
                logger.LogMessage("Make sure you are using the latest LiveSharp.Server tool package (> 2.0.0)");
                
                return new ServerInfo.Missing();
            }
        }

        static IHandshakeClient GetHandshake(ILiveSharpTransport transport)
        {
            if (transport is SocketTransport)
                return new TcpClientHandshake();
            if (transport?.GetType().Name == "TestHarnessTransport") {
                return new TestHarnessHandshake();
            }
            return new HttpClientHandshake();
        }
    }
}