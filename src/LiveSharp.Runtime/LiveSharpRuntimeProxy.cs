using LiveSharp.Runtime.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using LiveSharp.ServerClient;
using LiveSharp.Runtime.Network;
using System.Reflection;

namespace LiveSharp.Runtime
{
    public class LiveSharpRuntimeProxy : ILiveSharpRuntime
    {
        private readonly LiveServerClient _client;
        private readonly List<CodeUpdateHandler> _codeUpdateHandlers = new();
        private readonly List<ResourceUpdateHandler> _resourceUpdateHandlers = new();
        private readonly List<Action> _serverConnectedHandlers = new();
        private readonly List<Action<string>> _serverConnectedHandlersWithUrl = new();
        private readonly List<Action<Assembly>> _assemblyUpdateHandlers = new();
        private readonly List<Action<ILiveSharpLoadContext>> _assemblyLoadContextHandlers = new();
        private readonly ConcurrentDictionary<Type, MethodCallInterceptor> _methodCallInterceptors = new();
        private readonly ConcurrentDictionary<Type, ILiveSharpUpdateHandler> _defaultUpdateHandlers = new();
        private readonly ConcurrentDictionary<Type, bool> _handlerTypes;

        private bool _serverConnected;
        private readonly ConcurrentDictionary<string, string> _cachedPanelUpdates = new();
        private readonly DashboardUpdateHandler _dashboardUpdateHandler;
        private readonly LiveSharpAssemblyContextRegistry _assemblyContextRegistry;
        private readonly ConstructorInfo _assemblyLoadContextCtor;
        private string _serverUrl;

        public ILiveSharpLogger Logger { get; }
        public ILiveSharpConfig Config { get; }
        public LiveSharp.Runtime.Api.ProjectInfo ProjectInfo { get; }

        public LiveSharpRuntimeProxy(ILiveSharpLogger logger, ILiveSharpConfig config, LiveServerClient client,
            ConcurrentDictionary<Type, bool> handlerTypes, ConcurrentDictionary<string, ILiveSharpDashboard> liveSharpDashboards, ProjectInfo liveSharpProjectInfo,
            LiveSharpAssemblyContextRegistry assemblyContextRegistry)
        {
            _assemblyContextRegistry = assemblyContextRegistry;
            _client = client;
            _handlerTypes = handlerTypes;
            _dashboardUpdateHandler = new DashboardUpdateHandler(liveSharpDashboards, this);

            if (liveSharpDashboards != null) {
                var alc = liveSharpDashboards.GetType().Assembly.GetType("LiveSharp.LiveSharpAssemblyLoadContext");

                if (alc != null) {
                    _assemblyLoadContextCtor = alc.GetConstructor(new[] {typeof(LiveSharpAssemblyUpdate), typeof(IEnumerable<LiveSharpAssemblyUpdate>)});

                    if (_assemblyLoadContextCtor == null)
                        logger.LogWarning("LiveSharpAssemblyLoadContext doesn't have a suitable constructor");
                } else {
                    //logger.LogMessage("AssemblyLoadContext support is not implemented for this platform yet, only method updates will work");
                }
            }

            Logger = logger;
            Config = config;
            ProjectInfo = liveSharpProjectInfo;

            // TestHarness is a special case that we need to attach automatically
            if (_handlerTypes.Keys.FirstOrDefault(t => t.Name == "TestHarnessUpdateHandler") is var testHarnessHandler and not null) {
                AttachUpdateHandler(testHarnessHandler.Name);
            }
        }
        
        public IUpdatedMethod GetMethodUpdate(Type declaringType, string methodName, params Type[] parameterTypes)
        {
            var methodIdentifier = LiveSharpRuntime.GetMethodIdentifier(declaringType, methodName, parameterTypes);

            if (_assemblyContextRegistry.GetMethodUpdate(declaringType.Assembly, methodIdentifier, out var vmi))
                return new UpdatedMethodContext(methodIdentifier, vmi, declaringType);

            return null;
        }

        public Type GetTypeByFullName(string fullName)
        {
            return KnownTypes.FindType(fullName, null, false);
        }

        public void UseDefaultBlazorHandler()
        {
            AttachUpdateHandler("BlazorUpdateHandler");
        }

        public void UseDefaultXamarinFormsHandler(string hotReloadMethodName = "Build")
        {
            Config.SetValue("hotReloadMethodName", hotReloadMethodName);
            AttachUpdateHandler("XamarinFormsViewHandler");
            AttachUpdateHandler("XamarinFormsViewModelHandler");
        }

        public void UseDefaultUnoHandler(string hotReloadMethodName = "Build")
        {
            Config.SetValue("hotReloadMethodName", hotReloadMethodName);
            AttachUpdateHandler("UnoUpdateHandler");
        }

        public MethodCallInterceptor GetCallInterceptor(Type type, string methodIdentifier)
        {
            if (_methodCallInterceptors.TryGetValue(type, out var interceptor)) {
                if (interceptor.MethodName == null)
                    return interceptor;
                if (methodIdentifier.IndexOf(" " + interceptor.MethodName + " ", StringComparison.Ordinal) > -1)
                    return interceptor;
            }

            if (type.BaseType != null && type.BaseType != typeof(object))
                return GetCallInterceptor(type.BaseType, methodIdentifier);

            return null;
        }

        public void OnMethodCallIntercepted(Type declaringType, string methodName, MethodCallHandler callHandler)
        {
            OnMethodCallIntercepted(declaringType, methodName, callHandler, null);
        }

        public void OnMethodCallIntercepted(Type declaringType, string methodName, MethodCallHandler callHandler, Type excludeType)
        {
            try {
                if (declaringType == null)
                    throw new ArgumentNullException(nameof(declaringType));

                _assemblyContextRegistry.CreateCallInterceptors(declaringType, methodName, callHandler, excludeType);
            } catch (Exception e) {
                Logger.LogError("Setting interceptor failed", e);
            }
        }

        public void OnMethodCallIntercepted(Type declaringType, MethodCallHandler callHandler)
        {
            OnMethodCallIntercepted(declaringType, null, callHandler);
        }

        public void OnMethodCallIntercepted(Type declaringType, MethodCallHandler callHandler, Type excludeType)
        {
            OnMethodCallIntercepted(declaringType, null, callHandler, excludeType);
        }

        public void OnCodeUpdateReceived(CodeUpdateHandler updateHandler)
        {
            lock (_codeUpdateHandlers) {
                _codeUpdateHandlers.Add(updateHandler);
            }
        }

        public void OnResourceUpdateReceived(ResourceUpdateHandler resourceUpdateHandler)
        {
            lock (_resourceUpdateHandlers) {
                _resourceUpdateHandlers.Add(resourceUpdateHandler);
            }
        }

        public void OnAssemblyUpdateReceived(Action<Assembly> assemblyUpdateHandler)
        {
            lock (_assemblyUpdateHandlers) {
                _assemblyUpdateHandlers.Add(assemblyUpdateHandler);
            }
        }

        public void AssemblyUpdateReceived(Assembly updatedAssembly)
        {
            lock (_assemblyUpdateHandlers) {
                foreach (var handler in _assemblyUpdateHandlers)
                    handler(updatedAssembly);
            }
        }

        public void OnAssemblyLoadContextCreated(Action<ILiveSharpLoadContext> assemblyLoadContextHandler)
        {
            lock (_assemblyLoadContextHandlers) {
                _assemblyLoadContextHandlers.Add(assemblyLoadContextHandler);
            }
        }

        public void OnServerConnected(Action handler)
        {
            if (_serverConnected)
                handler();
            else {
                lock (_serverConnectedHandlers) {
                    _serverConnectedHandlers.Add(handler);
                }
            }
        }

        public void OnServerConnected(Action<string> handler)
        {
            if (_serverConnected)
                handler(_serverUrl);
            else {
                lock (_serverConnectedHandlersWithUrl) {
                    _serverConnectedHandlersWithUrl.Add(handler);
                }
            }
        }

        public void UpdateDiagnosticPanel(string panelName, string content)
        {
            if (_serverConnected) {
                var base64Content = Convert.ToBase64String(Encoding.Unicode.GetBytes(content));
                var message =
                    new XElement("PanelUpdate", new XAttribute("name", panelName), new XText(base64Content)).ToString(
                        SaveOptions.DisableFormatting);

                _client.SendBroadcast(message, ContentTypes.Inspector.PanelUpdate, BroadcastGroups.Inspector);
            } else {
                _cachedPanelUpdates[panelName] = content;
            }
        }

        private void ClearDiagnosticPanels()
        {
            _client.SendBroadcast("", ContentTypes.Inspector.PanelsClear, BroadcastGroups.Inspector);
        }

        public void CodeUpdateReceived(List<IUpdatedMethod> updatedMethods)
        {
            lock (_codeUpdateHandlers) {
                _dashboardUpdateHandler.CodeUpdated(updatedMethods);

                foreach (var codeUpdateHandler in _codeUpdateHandlers) {
                    codeUpdateHandler(updatedMethods);
                }
            }
        }

        public void ResourceUpdated(string path, string content)
        {
            lock (_resourceUpdateHandlers) {
                foreach (var resourceUpdateHandler in _resourceUpdateHandlers) {
                    resourceUpdateHandler(path, content);
                }
            }
        }

        public void ServerConnected(string serverUrl)
        {
            _serverConnected = true;
            _serverUrl = serverUrl;
            
            Task.Delay(3000).ContinueWith(_ => {
                lock (_serverConnectedHandlers) {
                    foreach (var serverConnectedHandler in _serverConnectedHandlers)
                        serverConnectedHandler();
                }
                lock (_serverConnectedHandlersWithUrl) {
                    foreach (var serverConnectedHandler in _serverConnectedHandlersWithUrl)
                        serverConnectedHandler(_serverUrl);
                }
            });
        }

        private void AttachUpdateHandler(string handlerTypeName)
        {
            var handlerType = _handlerTypes.Keys.FirstOrDefault(t => t.Name == handlerTypeName);

            if (handlerType == null) {
                Logger.LogError($"{handlerTypeName} was not registered");
                return;
            }

            var handler = _defaultUpdateHandlers.GetOrAdd(handlerType,
                t => (ILiveSharpUpdateHandler)Activator.CreateInstance(handlerType));

            handler.Attach(this);
        }

        public void ClearHandlers()
        {
            foreach (var defaultUpdateHandler in _defaultUpdateHandlers) {
                defaultUpdateHandler.Value.Dispose();
            }

            ClearDiagnosticPanels();

            lock (_codeUpdateHandlers)
                _codeUpdateHandlers.Clear();
            lock (_resourceUpdateHandlers)
                _resourceUpdateHandlers.Clear();
            lock (_serverConnectedHandlers)
                _serverConnectedHandlers.Clear();

            _methodCallInterceptors.Clear();

            lock (_assemblyUpdateHandlers)
                _assemblyUpdateHandlers.Clear();
        }

        public void MainAssemblyUpdated(LiveSharpAssemblyUpdate mainAssemblyUpdate, IEnumerable<LiveSharpAssemblyUpdate> referenceAssemblyUpdates)
        {
            if (_assemblyLoadContextCtor != null) {
                var assemblyLoadContext = (ILiveSharpLoadContext)_assemblyLoadContextCtor.Invoke(new object[] {mainAssemblyUpdate, referenceAssemblyUpdates});

                _assemblyContextRegistry.AddAssemblyLoadContext(assemblyLoadContext);

                lock (_assemblyLoadContextHandlers) {
                    foreach (var handler in _assemblyLoadContextHandlers)
                        handler(assemblyLoadContext);
                }
            }
        }

        public class MethodCallInterceptor
        {
            public MethodCallInterceptor(Type declaringType, string methodName, MethodCallHandler @delegate)
            {
                DeclaringType = declaringType;
                MethodName = methodName;
                Delegate = @delegate;
            }

            public MethodCallInterceptor(Type declaringType, MethodCallHandler @delegate)
            {
                DeclaringType = declaringType;
                Delegate = @delegate;
            }

            public Type DeclaringType { get; set; }
            public string MethodName { get; set; }
            public MethodCallHandler Delegate { get; set; }
        }
    }
}