using LiveSharp.Runtime.Api;
using LiveSharp.Runtime.Debugging;
using LiveSharp.Runtime.Handshake;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using LiveSharp.Runtime.Virtual;
using System.Reflection;
using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Network;
using LiveSharp.Runtime.Parsing;

namespace LiveSharp.Runtime
{
    public static class LiveSharpRuntime
    {
        private static Action<byte[], int> _liveXamlbufferFeeder;

        private static bool _isInitialized;
        private static LiveServerClient _client;
        private static LiveSharpRuntimeProxy _proxy;
        private static DocumentMetadata _latestUpdate;
        private static ILiveSharpConfig _config = new LiveSharpConfig();
        private static ConcurrentDictionary<Type, bool> _handlerTypes = new();
        private static ConcurrentDictionary<string, LiveSharpAssemblyUpdate> _referenceAssemblyUpdates = new();
        private static LiveSharpAssemblyUpdate _mainAssemblyUpdate;
        private static ConcurrentDictionary<string, ILiveSharpDashboard> _dashboards = new ();
        
        public static LiveSharpAssemblyContextRegistry AssemblyContextRegistry { get; } = new();
        public static ILogger Logger { get; set; } = new RuntimeLogger();
        public static Type DelegateCacheType { get; private set; }
        public static ILiveSharpRuntimeExt RuntimeExtensions { get; private set; } 

        public static MulticastDelegate MulticastDelegateForBuildTaskDontRemove; 
        public static AsyncCallback AsyncCallbackForBuildTaskDontRemove; 
        public static IAsyncResult IAsyncResultForBuildTaskDontRemove;
        private static LiveSharpLogger _liveSharpLoggerWrapper;

        public static string ProjectName { get; set; }

        public static void Start(string ip, string solutionPath, string projectName, string projectDir, string projectReferences, string nuGetPackagePath, Type liveXamlPluginType = null, ILiveSharpTransport transport = null, ILiveSharpDashboard dashboard = null)
        {
            if (dashboard != null) {
                _dashboards[projectName] = dashboard;
                
                if (_proxy != null)
                    dashboard.Configure(_proxy);
            }

            if (_isInitialized)
                return;

            Logger.LogMessage("LiveSharp runtime starting");
            
            _isInitialized = true;
            _liveSharpLoggerWrapper = new LiveSharpLogger(Logger);
            
            ProjectName = projectName;

            // Use Console for Blazor WASM
            if (Logger is RuntimeLogger runtimeLogger)
                runtimeLogger.UseConsoleWriteLine = transport != null;

            LoadExtensions();
            
            transport ??= new SocketTransport();
            var handshakeHost = transport.GetHandshakeHost(ip);
            
            _client = new LiveServerClient(Logger, MessageReceived, transport);

            var projectInfo = new ProjectInfo {
                SolutionPath = solutionPath.TrimEnd('\\'),
                ProjectName = projectName,
                ProjectDir = projectDir.TrimEnd('\\'),
                ProjectReferences = projectReferences,
                NuGetPackagePath = nuGetPackagePath.TrimEnd('\\'),
                IsLiveBlazor = _handlerTypes.Any(t => t.Key.Name.StartsWith("LiveBlazor"))
            };
            
            _proxy = new LiveSharpRuntimeProxy(_liveSharpLoggerWrapper, _config, _client, _handlerTypes, _dashboards, projectInfo, AssemblyContextRegistry);

            foreach (var liveSharpDashboard in _dashboards.Values)
                liveSharpDashboard.Configure(_proxy);

            // LiveBlazor.Dashboard has LiveSharp.Runtime injected but doesn't need to connect
            // it feeds all of the updates in-process
            if (projectInfo.ProjectName != "LiveBlazor.Dashboard" && !string.IsNullOrWhiteSpace(ip)) {
                ServerHandshake
                    .ConnectToServer(handshakeHost, projectInfo, transport, Logger)
                    .ContinueWith(task => {
                        if (!task.IsFaulted) {
                            var serverInfo = task.Result;

                            if (serverInfo is ServerInfo.Found found) {
                                _client?.Connect(found.ServerAddress, ip, () => {
                                    try {
                                        if (Logger is RuntimeLogger l)
                                            l.SetServerClient(_client);
                                        else
                                            Logger.LogError("LiveSharpRuntime Start method called and Logger is not RuntimeLogger");

                                        SendProjectInfo(solutionPath, projectName, projectDir, nuGetPackagePath, projectReferences, _client);

                                        Logger.LogMessage("LiveSharp Server connected");

                                        Trace.Listeners.Add(new LiveSharpTraceListener((RuntimeLogger)Logger));

                                        _proxy?.ServerConnected(found.ServerAddress.Url);

                                        LiveSharpDebugger.EventsReady += LiveSharpDebuggerOnEventsReady;
                                        LiveSharpDebugger.StartSending();
                                    } catch (Exception e) {
                                        Logger.LogError("Initialization failed\n" + e);
                                    }
                                }, _liveSharpLoggerWrapper).ContinueWith(_ => {});
                            }
                        }
                    });
            }

            if (liveXamlPluginType != null)
                StartLiveXamlPlugin(liveXamlPluginType);
            
            LoadExtensions();
        }

        private static void LiveSharpDebuggerOnEventsReady(DebugEvent[] events)
        {
            var serializedEvents = Serialize.ObjectArray(events, new DebugEventParser());
            _client.SendBroadcast(serializedEvents.ToArray(), ContentTypes.Inspector.DebugEvents, BroadcastGroups.Inspector);
        }

        private static void LoadExtensions()
        {
            try {
                return;
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LiveSharp.Runtime.Resources.LiveSharp.Runtime.NS21.dll");

                if (stream == null) {
                    Logger.LogMessage("LiveSharp extensions not found");
                    return;
                }          
            
                var assemblyData = new byte[stream.Length];
                stream.Read(assemblyData, 0, assemblyData.Length);
                var assembly = Assembly.Load(assemblyData);

                var extensionsType = assembly.GetTypes().FirstOrDefault(t => typeof(ILiveSharpRuntimeExt).IsAssignableFrom(t));
                if (extensionsType == null) {
                    Logger.LogMessage("Couldn't find type implementing ILiveSharpRuntimeExt in runtime extensions");
                    return;
                }

                RuntimeExtensions = (ILiveSharpRuntimeExt)Activator.CreateInstance(extensionsType);
            } catch (Exception e) {
                Logger.LogMessage("Loading extensions failed: " + e.Message);
            }
        }

        public static void AddHandler(Type handlerType)
        {
            _handlerTypes[handlerType] = false;
        }

        public static void AddSetting(string key, string value)
        {
            _config.SetValue(key, value);
        }
        
        public static void SetDashboardUrl(string dashboardUrl)
        {
            _proxy?.ServerConnected(dashboardUrl);
        }
        
        public static void AddDelegateFieldMapping(Type declaringType, string methodName, string methodIdentifier, Type fieldHost, string fieldName, Type returnType, Type[] parameterTypes)
        {
            AssemblyContextRegistry.AddDelegateFieldMapping(declaringType, methodName, methodIdentifier, fieldHost, fieldName, returnType, parameterTypes);
        }
        
        private static void SendProjectInfo(string solutionPath, string projectName, string projectDir,
            string nuGetPackagePath, string projectReferences, LiveServerClient client)
        {
            var projectInfoXml = $@"<ProjectInfo>
                                        <SolutionPath>{solutionPath}</SolutionPath>
                                        <ProjectName>{projectName}</ProjectName>
                                        <ProjectDir>{projectDir}</ProjectDir>
                                        <ProjectReferences>{projectReferences}</ProjectReferences>
                                        <NuGetPackagePath>{nuGetPackagePath}</NuGetPackagePath>
                                    </ProjectInfo>";

            client.SendBroadcast(projectInfoXml, ContentTypes.General.ProjectInfoXml, BroadcastGroups.General);
        }

        private static void StartLiveXamlPlugin(Type liveXamlPluginType)
        {
            if (liveXamlPluginType == null) throw new ArgumentNullException(nameof(liveXamlPluginType));
            
            var liveXamlPluginCtor = liveXamlPluginType.GetConstructors().First();
            var getBufferFeederMethod = liveXamlPluginType.GetMethod("GetBufferFeeder");

            if (liveXamlPluginCtor != null && getBufferFeederMethod != null) {
                var liveXamlPlugin = liveXamlPluginCtor.Invoke(new object[0]);
                _liveXamlbufferFeeder = (Action<byte[], int>)getBufferFeederMethod.Invoke(liveXamlPlugin, new object[0]);
            }
        }

        private static string GetMethodSignature(params Type[] argumentTypes)
        {
            var result = "";
            for (int i = 0; i < argumentTypes.Length; i++) {
                result += argumentTypes[i].ToString();
                if (i < argumentTypes.Length - 1)
                    result += " ";
            }

            return result;
        }

        public static string GetMethodIdentifier(Type ownerType, string methodName, Type[] argumentTypes)
        {
            return GetMethodIdentifier(ownerType, methodName, GetMethodSignature(argumentTypes));
        }

        internal static string GetMethodIdentifier(Type type, string methodName, string signature)
        {
            return GetMethodIdentifier(type.FullName, methodName, signature);
        }

        internal static string GetMethodIdentifier(string type, string methodName, string signature)
        {
            return type + " " + methodName + " " + signature;
        }

        public static void MessageReceived(ServerMessage message)
        {
            Logger.LogDebug("Message received: " + message);

            if (message.Parameter == BroadcastGroups.LiveSharp)
                LiveSharpMessageReceived(message);
            else if (message.Parameter == BroadcastGroups.LiveXaml)
                LiveXamlMessageReceived(message);
        }

        private static void LiveXamlMessageReceived(ServerMessage message)
        {
            Logger.LogMessage("received XAML update");

            _liveXamlbufferFeeder?.Invoke(message.Content, message.Content.Length);
        }

        private static void LiveSharpMessageReceived(ServerMessage message)
        {
            var contentType = message.ContentType;

            LiveSharpMessageReceived(message, contentType, message.Parameter);
        }

        public static void LiveSharpMessageReceived(ServerMessage serverMessage, byte contentType, int groupId)
        {
            try {
                if (contentType == ContentTypes.LiveSharp.DocumentElement) {
                    var messageContent = serverMessage.GetContentText();
                    var documentElement = XElement.Parse(messageContent, LoadOptions.PreserveWhitespace);
                    
                    _latestUpdate = UpdateDocument(documentElement);
                    
                    Logger.LogMessage("Received code update");
                } else if (contentType == ContentTypes.LiveSharp.EnableDebugLogging) {
                    var messageContent = serverMessage.GetContentText();
                    
                    if (bool.TryParse(messageContent, out var debugLoggingEnabled)) {
                        Logger.IsDebugLoggingEnabled = debugLoggingEnabled;
                        Logger.LogMessage("Debug logging enabled: " + debugLoggingEnabled);
                    }
                    else {
                        Logger.LogError($"Invalid {nameof(ContentTypes.LiveSharp.EnableDebugLogging)} format: " +
                                        messageContent);
                    }
                } else if (contentType == ContentTypes.LiveSharp.ResourceUpdated) {
                    var messageContent = serverMessage.GetContentText();
                    
                    Logger.LogMessage("Received resource update");
                    
                    var documentElement = XElement.Parse(messageContent, LoadOptions.PreserveWhitespace);
                    var resourcePath = documentElement.AttributeValueOrThrow("path");
                    var content = documentElement.Element("Content")?.Value;

                    _proxy?.ResourceUpdated(resourcePath, content);
                } else if (contentType == ContentTypes.LiveSharp.AssemblyUpdate) {
                    Logger.LogMessage("Received assembly update");
                    
                    var assemblyBuffer = serverMessage.Content;
                    var assemblyUpdate = Deserialize.Object<LiveSharpAssemblyUpdate>(assemblyBuffer);

                    if (assemblyUpdate.Name == ProjectName) {
                        _mainAssemblyUpdate = assemblyUpdate;
                        _proxy.MainAssemblyUpdated(_mainAssemblyUpdate, _referenceAssemblyUpdates.Values);
                    } else {
                        _referenceAssemblyUpdates[assemblyUpdate.Name] = assemblyUpdate;
                    }
                    
                    //new LiveSharpAssemblyUpdate()
                    // var updatedAssembly = AssemblyContextRegistry.AddAssembly(Assembly.Load(assemblyBuffer));
                }
            }
            catch (Exception e) {
                Logger.LogError(e.ToString());
            }
        }

        public static DocumentMetadata UpdateDocument(XElement element, Func<string, bool> methodFilter = null, bool debuggingEnabled = false)
        {
            try {
                var assemblyName = element.AttributeValueOrThrow("AssemblyName");
                var assemblyContexts = AssemblyContextRegistry.GetAssemblyContexts(assemblyName);
                var documentMetadatas = assemblyContexts.Select(ctx => ctx.Update(element, methodFilter, debuggingEnabled)).ToArray();
                var updateContext = new List<IUpdatedMethod>();

                var updatedMethodContexts = documentMetadatas
                    .SelectMany(dm => dm.UpdatedMethods)
                    .Select(m => new UpdatedMethodContext(m.MethodIdentifier, m, CompilerHelpers.ResolveVirtualType(m.DeclaringType)));
                updateContext.AddRange(updatedMethodContexts);
            
                _proxy?.CodeUpdateReceived(updateContext);

                return documentMetadatas.LastOrDefault();
            }
            catch (Exception) {
                Logger.LogError("UpdateDocument failed: " + Environment.NewLine + element);
                throw;
            }
        }
        
        public static bool GetMethodUpdate(Assembly assembly, string methodIdentifier, out VirtualMethodInfo vmi)
        {
            return AssemblyContextRegistry.GetMethodUpdate(assembly, methodIdentifier, out vmi);
        }
    }
}