using LiveSharp.Shared.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace LiveSharp.Dashboard
{
    internal static class ServerHandshake
    {
        static readonly int[] ServerPortRange = {50540, 30540, 40540};
        private static int _connectionPort;
        private static ProjectInfo _projectInfo;
        public static async Task ConnectToServer(ProjectInfo projectInfo, string serverAddress, int tcpServerPort, ILogger logger)
        {
            try {
                using var client = new HttpClient();
                
                for (int i = 0; i < 3; i++) {
                    foreach (var port in ServerPortRange) {
                        var url = $"http://localhost:{port}/connect/workspace";
                        try {
                            logger.LogMessage($"Connecting to {url}");
                        
                            var formUrlEncodedContent = new FormUrlEncodedContent(new[] {
                                new KeyValuePair<string, string>("projectId", projectInfo.GetProjectId()),
                                new KeyValuePair<string, string>("serverAddress", serverAddress),
                                new KeyValuePair<string, string>("tcpServerPort", tcpServerPort.ToString()),
                            });
                        
                            await client.PostAsync(url, formUrlEncodedContent);

                            _connectionPort = port;
                            _projectInfo = projectInfo;
                            
                            return;
                        } catch (HttpRequestException e) {
                            logger.LogError($"Unable to connect to {url}", e);
                        }
                    }
                }

                throw new Exception("LiveSharp Server not found at ports 50540, 30540, 40540");
            } catch (Exception e) {
                logger.LogError("Connecting to LiveSharp server failed", e);
            }
        }

        public static async Task SendWorkspaceLogMessage(string logMessage)
        {
            await SendLogMessage(logMessage, "workspacelog");
        }

        public static async Task SendRuntimeLogMessage(string logMessage)
        {
            await SendLogMessage(logMessage, "runtimelog");
        }
        
        private static async Task SendLogMessage(string logMessage, string endpoint)
        {
            try {
                using var client = new HttpClient();

                var url = $"http://localhost:{_connectionPort}/connect/{endpoint}";

                var formUrlEncodedContent = new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string, string>("projectName", _projectInfo?.ProjectName),
                    new KeyValuePair<string, string>("logText", logMessage)
                });

                await client.PostAsync(url, formUrlEncodedContent);
            }
            catch (Exception e) {
                // Don't post to Logger so we don't create an infinite logging loop if connection fails
                Console.WriteLine();
            }
        }
    }
}