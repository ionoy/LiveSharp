using LiveSharp.Shared.Network;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSharp
{
    class Program
    {
        //private static WorkspaceLogger _logger;
        private static LiveServerClient _client;
        private static TimeSpan PingDelay = TimeSpan.FromSeconds(20);
        private static AutoResetEvent _autoResetEvent;
        private static bool _heartBeatReceived;

        public static void Main (string[] args) 
        {
            //_logger = new WorkspaceLogger();

            // try
            // {
            //     _client = new LiveServerClient("127.0.0.1", new [] {50540, 52540, 54540, 56540, 58540}, _logger, MessageReceived);
            //     _client.Connect(() =>
            //     {
            //         _client.JoinGroup(BroadcastGroups.Heartbeat);
            //
            //         _logger.SetServerClient(_client);
            //
            //     }).ContinueWith(_ => { });
            //
            //     if (args.Length != 4)
            //     {
            //         var argsReceived = string.Join(" ", args);
            //         throw new Exception("Invalid arguments. Usage: LiveSharp.Workspace.exe {path-to-nuget} {path-to-sln} {project-dir} {project-name}" + Environment.NewLine +
            //                             "Received: " + argsReceived);
            //     }
            //
            //     var nugetPath = args[0];
            //     var solutionPath = args[1];
            //     var projectDir = args[2];
            //     var projectName = args[3];
            //     var eventBus = new EventBus();
            //
            //     eventBus.Events.Subscribe(BroadcastEvent);
            //
            //     //var workspace = new LiveSharpWorkspace(_logger, eventBus, SendBroadcast);
            //
            //     //workspace.LoadSolution(nugetPath, solutionPath, projectDir, projectName);
            //     
            //     CheckForHeartbeatsAsync();
            //
            //     _autoResetEvent = new AutoResetEvent(false);
            //     _autoResetEvent.WaitOne();
            // }
            // catch (Exception e)
            // {
            //     _logger.LogError(e.ToString());
            //     return;
            // }
        }

        // private static void BroadcastEvent(Event evt)
        // {
        //     _client.SendBroadcast(evt.Serialize(), ContentTypes.General.Event, BroadcastGroups.General);
        // }
        //
        // private static void CheckForHeartbeatsAsync()
        // {
        //     Task.Run(async () => {
        //         while (true)
        //         {
        //             await Task.Delay(15000);
        //
        //             if (_heartBeatReceived)
        //             {
        //                 Console.WriteLine("Heartbeat received in previous 15 seconds");
        //                 _heartBeatReceived = false;
        //             } 
        //             else
        //             {
        //                 _logger.LogMessage("Exiting because heartbeat was not received in 15 seconds");
        //                 
        //                 // Wait for pending file operations, we are not in a rush
        //                 await Task.Delay(100);
        //
        //                 _client.Dispose();
        //                 _autoResetEvent.Set();
        //             }
        //         }
        //     });
        // }
        //
        // private static void SendBroadcast(byte[] buffer, byte contentType, int broadcastGroup)
        // {
        //     _client.SendBroadcast(buffer, contentType, broadcastGroup);
        // }
        //
        // private static void MessageReceived(ServerMessage message)
        // {
        //     if (message.Parameter == BroadcastGroups.Heartbeat)
        //         _heartBeatReceived = true;
        // }
    }
}
