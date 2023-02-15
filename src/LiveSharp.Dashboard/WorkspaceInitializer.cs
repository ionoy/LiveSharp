using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using LiveSharp.Dashboard.Services;
using LiveSharp.Shared.Debugging;
using LiveSharp.Shared.Network;
using LiveSharp.Shared.Parsing;
using LiveSharp.Shared.Api;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;

namespace LiveSharp.Dashboard
{
    public class WorkspaceInitializer
    {
        private ILogger _logger;
        public static DebuggingService _debuggingService { get; private set; }
        public static LiveSharpWorkspace Workspace { get; private set; }
        
        private bool _isLicenseValid;
        private BlazorHubClient _blazorHubClient;
        private LiveHost _server;
        internal ProjectInfo ProjectInfo { get; set; }
        
        public WorkspaceInitializer(string nugetPath, string solutionPath, string projectDir, string projectName)
        {
            ProjectInfo = new ProjectInfo {
                NuGetPackagePath = nugetPath,
                ProjectDir = projectDir,
                ProjectName = projectName,
                ProjectReferences = "",
                SolutionPath = solutionPath
            };
        }

        public void Start(ILogger logger, string serverAddress, DebuggingService debuggingService, IHubContext<BlazorHub> blazorHubContext)
        {
            _logger = logger;
            _debuggingService = debuggingService;
            
            //CreateWorkspace();
            StartServer(blazorHubContext);
            
            ServerHandshake.ConnectToServer(ProjectInfo, serverAddress, _server.GetAssignedPort(), logger)
            .ContinueWith(task => {
                
            });
            
            CreateWorkspace();
        }

        private void CreateWorkspace()
        {
            Workspace?.Dispose();
            Workspace = new LiveSharpWorkspace(_logger, SendBroadcast);
            Workspace.LoadSolution(ProjectInfo);
            Workspace.SetLicenseStatus(true);
            
            Task.Run(async () => await CheckForNuGetUpdates());
        }

        private void SendBroadcast(byte[] buffer, byte contentType, int groupId)
        {
            var serverMessage = new ServerMessage(buffer, contentType, MessageType.Broadcast, groupId);
            _blazorHubClient.Send(serverMessage.CreateBuffer());
            _server.SendBroadcast(serverMessage);
        }

        private void MessageReceived(ServerMessage message, INetworkClient client)
        {
            //_logger.LogMessage("Message received: " + message.GetContentText());
            
            if (message.Parameter == BroadcastGroups.General) {
                if (message.ContentType == ContentTypes.General.ProjectInfoXml) {
                    ProjectInfoReceived(message);
                } else if (message.ContentType == ContentTypes.General.RuntimeLog) {
                    //_logger.LogMessage(message.GetContentText());
                    var time = DateTime.Now.ToString("HH:mm:ss.fff") + ": ";
                    ServerHandshake.SendRuntimeLogMessage(time + message.GetContentText()).ConfigureAwait(false);
                }
            } else if (message.Parameter == BroadcastGroups.Inspector) {
                if (message.ContentType == ContentTypes.Inspector.MethodWatch) {
                    _debuggingService.NewMethodWatch(message.GetContentText());
                } else if (message.ContentType == ContentTypes.Inspector.MethodWatchStart) {
                    _debuggingService.NewMethodStart(message.GetContentText());
                } else if (message.ContentType == ContentTypes.Inspector.MethodWatchEnd) {
                    _debuggingService.NewMethodEnd(message.GetContentText());
                } else if (message.ContentType == 80) { //ContentTypes.Inspector.PanelUpdate (replace with the field after >1.6.6.0 update)
                    var xdoc = XDocument.Parse(message.GetContentText(), LoadOptions.PreserveWhitespace);
                    var panelUpdateElement = xdoc.Element("PanelUpdate");
                    if (panelUpdateElement == null)
                        throw new InvalidOperationException("Invalid Panel Update message received: " + message.GetContentText());
                    
                    var panelName = panelUpdateElement.Attribute("name")?.Value;
                    var content = Encoding.Unicode.GetString(Convert.FromBase64String(panelUpdateElement.Value));
                    
                    _debuggingService.UpdatePanel(panelName, content);
                } else if (message.ContentType == ContentTypes.Inspector.PanelsClear) { 
                    _debuggingService.ClearPanels();
                } else if (message.ContentType == ContentTypes.Inspector.DebugEvents) {
                    var debugEvents = Deserialize.ObjectArray<DebugEvent>(message.Content, new DebugEventParser());
                    _debuggingService.FeedEvents(debugEvents);
                }
            } else if (message.Parameter == BroadcastGroups.Dashboard) {
                if (message.ContentType == ContentTypes.Dashboard.Quit) {
                    Quit();
                }
            }
        }
        private void ProjectInfoReceived(ServerMessage message)
        {
            try
            {
                var xml = message.GetContentText();
                var doc = XDocument.Parse(xml);
                var root = doc.Root;
                
                var solutionPath = getValue("SolutionPath")?.TrimEnd('\\');
                var projectName = getValue("ProjectName");
                var projectDir = getValue("ProjectDir")?.TrimEnd('\\');
                var nuGetPackagePath = getValue("NuGetPackagePath").TrimEnd('\\');

                // When building from csproj, the SolutionPath will be set to *Undefined*
                if (string.IsNullOrWhiteSpace(solutionPath) || solutionPath == "*Undefined*")
                    solutionPath = projectDir;
                
                ProjectInfo = new ProjectInfo {
                    NuGetPackagePath = nuGetPackagePath,
                    SolutionPath = solutionPath,
                    ProjectDir = projectDir,
                    ProjectName = projectName
                };
                
                CreateWorkspace();

                string getValue(string elementName) {
                    try {
                        return root.Descendants(elementName).First().Value;
                    } catch {
                        throw new Exception("Couldn't find " + elementName);
                    }
                }
            }
            catch (Exception e) 
            {
                _logger.LogError("ProjectInfo loading failed. " + Environment.NewLine + e);
            }
        }

        public void SetLicenseStatus(in bool isValid)
        {
            _isLicenseValid = isValid;
            Workspace.SetLicenseStatus(isValid);
        }
        
        private async Task CheckForNuGetUpdates()
        {
            try
            {
                var nugetPath = typeof(WorkspaceInitializer).Assembly.Location;
                var nugetDirectoryVersion = new DirectoryInfo(nugetPath).Parent.Parent.Parent.Name;
                
                if (nugetDirectoryVersion == "LiveSharp")
                    nugetDirectoryVersion = "1.0.0";
                
                var match = Regex.Match(Environment.CommandLine, @"\/ServerVersion=(\d+\.\d+\.\d)");
                if (match.Groups.Count == 0)
                    throw new Exception("ServerVersion argument couldn't be parsed");

                var serverVersionString = match.Groups[1].Value;
                Console.WriteLine("server: " + serverVersionString);
                var serverVersion = Version.Parse(serverVersionString);
                
                Console.WriteLine("nuget: " + nugetDirectoryVersion);
                var nuGetPackageVersion = Version.Parse(nugetDirectoryVersion);
                
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://api-v2v3search-0.nuget.org/");
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await client.GetAsync("query?q=livesharp");
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var jobject = JObject.Parse(jsonResponse);
                        var latestNugetPackageVersion = jobject.SelectToken("$.data[0].versions[-1:].version").Value<string>();
                        var latestNugetServerVersion = jobject.SelectToken("$.data[1].versions[-1:].version").Value<string>();

                        var defaultColor = Console.ForegroundColor;
                        if (Version.Parse(latestNugetPackageVersion) > nuGetPackageVersion) {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"LiveSharp NuGet package is outdated ({nuGetPackageVersion} < {latestNugetPackageVersion}). Make sure to update LiveSharp package in every project that uses it.");
                            Console.ForegroundColor = defaultColor;
                        }

                        if (Version.Parse(latestNugetServerVersion) > serverVersion) {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"LiveSharp server tool is outdated ({serverVersion} < {latestNugetServerVersion}). Please run `dotnet tool update livesharp.server --global` to update it.");
                            Console.ForegroundColor = defaultColor;
                        }
                        
                        return;
                    }

                    throw new Exception("NuGet API returned " + response.StatusCode + " " + response.Content);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to retrieve the latest NuGet package version" + Environment.NewLine + e);
            }
        }

        private void StartServer(IHubContext<BlazorHub> blazorHubContext)
        {
            try
            {
                _blazorHubClient = new BlazorHubClient(blazorHubContext);

                BlazorHub.Parser.MessageParsed += (sender, args) => MessageReceived(args.Message, _blazorHubClient);

                _server = new LiveHost(MessageReceived, ClientDisconnected, _logger);
                _server.Start();
            }
            catch (Exception e)
            {
                _logger.LogError("Starting HostServer failed", e);
            }
        }
        
        private void ClientDisconnected()
        {
            Quit();
        }
        
        private void Quit() 
        { 
            _logger.LogMessage("Dashboard process quitting");
            Process.GetCurrentProcess().Kill(); 
        }
    }
}