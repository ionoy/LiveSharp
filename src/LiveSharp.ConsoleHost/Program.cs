using LiveSharp.Shared.Network;
using System;
using System.Xml.Linq;

namespace LiveSharp.ConsoleHost
{
    class Program
    {
        private static ILogger _logger;
        private static LiveHost _server;

        static void Main(string[] args)
        {
            var hostPort = args.Length > 0 ? int.Parse(args[0]) : 50540;

            var newArr = new object[args.Length];

            for (int i = 0; i < args.Length; i++) {
                newArr[i] = args[i];
            }
            
            _logger = new ConsoleLogger();

            Start(hostPort);

            Console.ReadKey();
        }

        static void Start(int hostPort)
        {
            try {
                try {
                    _server = new LiveHost(HandleMessage, () => {}, _logger);
                } catch (Exception e) {
                    _logger.LogWarning($"Couldn't open at port {hostPort} because: {e.Message}");
                }

                if (_server == null) {
                    _logger.LogError("Couldn't start LiveServer'");
                    throw new Exception("Couldn't start LiveServer'");
                }
            } catch (Exception e) {
                _logger.LogError("Starting HostServer failed", e);
            }
        }

        private static void HandleMessage(ServerMessage message, INetworkClient client)
        {
            Console.WriteLine("Message received: " + message);

            if (message.Parameter == BroadcastGroups.General) {
                if (message.ContentType == ContentTypes.General.ProjectInfoXml) {
                    var xml = message.GetContentText();
                    var doc = XDocument.Parse(xml);
                    var root = doc.Root;

                    // if (root.Name == "ProjectInfo")
                    //     ProjectInfoReceived(root);
                } else if (message.ContentType == ContentTypes.General.RuntimeLog) {
                    var log = message.GetContentText();
                    Console.WriteLine("(runtime) " + log);
                }
            } else if (message.Parameter == BroadcastGroups.Inspector) {
                _server.SendBroadcast(message.Content, message.ContentType, message.Parameter);
            } else if (message.Parameter == BroadcastGroups.LiveSharp) {
                _server.SendBroadcast(message.Content, message.ContentType, message.Parameter);
            }
        }
    }

    class ConsoleLogger : ILogger
    {
        public bool IsDebugLoggingEnabled { get; set; }

        public void LogError(string errorMessage)
        {
            Write("Error: " + errorMessage);
        }

        public void LogError(string errorMessage, Exception e)
        {
            Write("Error: " + errorMessage);
            Write(e.ToString());
        }

        public void LogMessage(string message)
        {
            Write("Message: " + message);
        }

        public void LogWarning(string warning)
        {
            Write("Warning: " + warning);
        }

        public void LogDebug(string debugInfo)
        {
            if (IsDebugLoggingEnabled)
                Write("debug: " + debugInfo);
        }

        private void Write(string message)
        {
            Console.WriteLine(message);
        }
    }
}