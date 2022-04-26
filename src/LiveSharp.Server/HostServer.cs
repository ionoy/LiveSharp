// using System;
// using System.Diagnostics;
// using System.Linq;
// using System.Reflection;
// using System.Threading.Tasks;
// using System.Xml.Linq;
// using LiveSharp.Shared.Network;
// using LiveSharp.ServerClient;
// using Microsoft.AspNetCore.SignalR;
// using System.Collections.Concurrent;
// using System.Runtime.CompilerServices;
//
// namespace LiveSharp.Server
// {
//     public class HostServer
//     {
//         public static readonly int[] HostPorts = { 50540, 30540, 40540, 56540, 58540 };
//         
//         private readonly ILogger _logger;
//         private readonly ConcurrentDictionary<string, Process> _dashboardProcesses = new();
//
//         private LiveServer _server;
//         private string _currentProjectIdentity;
//         private BlazorHubClient _blazorHubClient;
//
//         private readonly ImmutableListDictionary<string, INetworkClient> _clientsByProject = new();
//
//         public static HostServer Instance { get; set; }
//
//
//         public HostServer(ILogger logger)
//         {
//             _logger = logger;
//
//             Instance = this;
//
//             AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
//                 foreach (var process in _dashboardProcesses.Values) {
//                     process?.Kill();
//                 }
//             };
//         }
//
//         public void StartServer(IHubContext<BlazorHub> blazorHubContext)
//         {
//             try
//             {
//                 _blazorHubClient = new BlazorHubClient(blazorHubContext);
//
//                 BlazorHub.Parser.MessageParsed += (sender, args) => HandleMessage(args.Message, _blazorHubClient);
//
//                 foreach (var port in HostPorts) {
//                     try {
//                         _server = new LiveServer(port, ClientJoinedGroup, ClientLeftGroup, _logger);
//                         break;
//                     } catch (Exception e) {
//                         _logger.LogWarning($"Couldn't open at port {port} because: {e.Message}");
//                     }
//                 }
//
//                 if (_server == null) {
//                     _logger.LogError("Couldn't start LiveServer'");
//                     throw new Exception("Couldn't start LiveServer'");
//                 }
//
//                 _server.RegisterMessageHandler(HandleMessage);
//
//                 // Give ASP.NET Core a chance to output starting messages first
//                 Task.Delay(TimeSpan.FromMilliseconds(100))
//                     .ContinueWith(_ => PrintWelcomeMessage());
//             }
//             catch (Exception e)
//             {
//                 _logger.LogError("Starting HostServer failed", e);
//             }
//         }
//
//         private void PrintWelcomeMessage()
//         {
//             var original = Console.ForegroundColor;
//             Console.WriteLine("");
//             Console.ForegroundColor = ConsoleColor.Green;
//             Console.WriteLine("Welcome to LiveSharp!");
//             Console.ForegroundColor = original;
//             Console.WriteLine(@"
// Documentation https://www.livesharp.net/help/
// Release information https://www.livesharp.net/news/
// Live support https://gitter.im/LiveSharp/Lobby (@ionoy)
// License purchasing https://www.livesharp.net/#licensing
// Log files locations are %TEMP%\LiveSharp on Windows and $TMPDIR/LiveSharp on OSX
//
// 1) Make sure 'LiveSharp' package is installed in the main project (the one with Program.Main or App.Initialize)
//    Also install 'LiveSharp' into other projects that should be hot-reloadable
// 2) Run the application 
//    If nothing appears here after application has started, check the debugging Output window for 'livesharp: ' messages
// ");
//         }
//
//         private void ClientLeftGroup(INetworkClient client, int groupId)
//         {
//             RemoveClient(client);
//         }
//
//         private void ClientJoinedGroup(INetworkClient client, int groupId)
//         {
//             if (groupId == BroadcastGroups.Dashboard) 
//                 AssignClientToProject(client, _currentProjectIdentity);
//         }
//
//         public void HandleMessage(ServerMessage message, INetworkClient client)
//         {
//             // Don't handle JoinGroup messages
//             if (message.MessageType != MessageType.Broadcast)
//                 return;
//             
//             // _logger.LogDebug("host: " + message.GetContentText());
//             
//             if (message.Parameter == BroadcastGroups.General && message.ContentType == ContentTypes.General.ProjectInfoXml) {
//                 var xml = message.GetContentText();
//                 var doc = XDocument.Parse(xml);
//                 var root = doc.Root;
//
//                 if (root?.Name == "ProjectInfo")
//                     ProjectInfoReceived(root, message, client);
//             }
//
//             SendBroadcast(message, client);
//         }
//
//         private void SendBroadcast(ServerMessage message, INetworkClient networkClient)
//         {
//             try {
//                 var projectIdentity = GetClientProjectIdentity(networkClient);
//                 if (_clientsByProject.TryGetList(projectIdentity, out var clients)) {
//                     if (message.ContentType != ContentTypes.General.RuntimeLog)
//                         _blazorHubClient.Send(message.CreateBuffer());
//
//                     _server.SendBroadcast(message, clients);
//                 } else {
//                     throw new InvalidOperationException($"No clients for project {projectIdentity}");
//                 }
//             } catch (Exception e) {
//                 _logger.LogError("SendBroadcast failed", e);
//             }
//         }
//
//         private string GetClientProjectIdentity(INetworkClient networkClient)
//         {
//             if (_clientsByProject.TryGetKey(networkClient, out var projectIdentity))
//                 return projectIdentity;
//             
//             throw new InvalidOperationException($"Couldn't find project identity for specific client");
//         }
//
//         private void ProjectInfoReceived(XElement root, ServerMessage message, INetworkClient networkClient)
//         {
//             try
//             {
//                 var solutionPath = getValue("SolutionPath")?.TrimEnd('\\');
//                 var projectName = getValue("ProjectName");
//                 var projectDir = getValue("ProjectDir")?.TrimEnd('\\');
//                 var nuGetPackagePath = getValue("NuGetPackagePath").TrimEnd('\\');
//
//                 // When building from csproj, the SolutionPath will be set to *Undefined*
//                 if (string.IsNullOrWhiteSpace(solutionPath) || solutionPath == "*Undefined*")
//                     solutionPath = projectDir;
//
//                 var projectIdentity = projectName + nuGetPackagePath;
//                 
//                 AssignClientToProject(networkClient, projectIdentity);
//
//                 if (_currentProjectIdentity == projectIdentity) {
//                     SendBroadcast(message, networkClient);
//                     return;
//                 }
//
//                 _currentProjectIdentity = projectIdentity;
//                 
//                 var currentProcess = Process.GetCurrentProcess();
//                 var processName = currentProcess.MainModule.FileName;
//                 var args = string.Join(" ", Program.Arguments);
//                 var version = typeof(HostServer).Assembly
//                                                 .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
//                                                 .InformationalVersion;
//                 
//                 args += $" /SolutionPath=\"{solutionPath}\"";
//                 args += $" /ProjectName=\"{projectName}\"";
//                 args += $" /ProjectDir=\"{projectDir}\"";
//                 args += $" /NuGetPackagePath=\"{nuGetPackagePath}\"";
//                 args += $" /ServerVersion=\"{version}\"";
//
//                 if (_logger.IsDebugLoggingEnabled)
//                     args += " debug";
//                 
//                 _logger.LogMessage("Starting dashboard process with arguments: " + args);
//                 
//                 //var hideDashboard = Program.Arguments.Any(a => a == "--hide-dashboard") ? " --hide-dashboard" : "";
//                 
//                 _dashboardProcesses[projectIdentity] = Process.Start(processName, $"{args} dashboard");
//             }
//             catch (Exception e) 
//             {
//                 _logger.LogError("ProjectInfo loading failed. " + Environment.NewLine + e);
//             }
//
//             string getValue(string elementName) {
//                 try {
//                     return root.Descendants(elementName).First().Value;
//                 } catch {
//                     throw new Exception("Couldn't find " + elementName);
//                 }
//             }
//         }
//         
//         private void RemoveClient(INetworkClient client)
//         {
//             if (_clientsByProject.TryGetKey(client, out var projectIdentity) &&
//                 _clientsByProject.TryGetList(projectIdentity, out var clients)) {
//                 clients = clients.Remove(client);
//
//                 _clientsByProject.UpdateList(projectIdentity, clients);
//             }
//         }
//         
//         private void AssignClientToProject(INetworkClient networkClient, string projectIdentity)
//         {
//             _clientsByProject.Add(projectIdentity, networkClient);
//         }
//     }
// }