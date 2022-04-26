using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace LiveSharp.Rewriters
{
    public class LiveSharpStartRewriter : RewriterBase
    {
        private readonly String _nuGetPackagePath;
        private readonly String _projectDir;
        private readonly String _projectName;
        private readonly String _solutionPath;
        private readonly String _projectReferences;
        private readonly RewriteLogger _logger;
        private readonly ModuleDefinition _mainModule;
        private readonly RuntimeMembers _runtimeMembers;

        public TypeDefinition LiveXamlPluginType { get; private set; }
        public TypeDefinition TransportType { get; private set; }
        public TypeDefinition DashboardType { get; set; }
        public InjectRule StartRule  { get; set; }
        public string ServerIp  { get; set; }
        public bool SkipStart { get; set; }
        
        public List<MethodDefinition> ClientStartMethods { get; } = new();
        public List<TypeDefinition> UpdateHandlerTypes { get; } = new();
        public Dictionary<string, string> Settings { get; } = new();

        public LiveSharpStartRewriter(RewriteLogger logger, ModuleDefinition mainModule, RuntimeMembers runtimeMembers, string nuGetPackagePath, string projectDir, string projectName, string solutionPath, string projectReferences)
        {
            _logger = logger;
            _mainModule = mainModule;
            _runtimeMembers = runtimeMembers;
            _nuGetPackagePath = nuGetPackagePath;
            _projectDir = projectDir;
            _projectName = projectName;
            _solutionPath = solutionPath;
            _projectReferences = projectReferences;
        }

        public override void ProcessSupportModule(ModuleDefinition supportModule)
        {
            if (supportModule == null)
                return;

            var types = supportModule.GetAllTypes().ToArray();
            var updateHandlerTypes = types.Where(t => t.HasInterface("LiveSharp.ILiveSharpUpdateHandler"));

            foreach (var updateHandlerType in updateHandlerTypes) {
                _logger.LogMessage("Update handler: " + updateHandlerType);
                UpdateHandlerTypes.Add(_mainModule.ImportReference(updateHandlerType).Resolve());
            }
            
            var transportType = types.FirstOrDefault(t => t.HasInterface("LiveSharp.Runtime.Network.ILiveSharpTransport"));
            if (transportType != null) {
                TransportType = _mainModule.ImportReference(transportType).Resolve();
            }
        }
        
        internal void ProcessLiveXamlModule(ModuleDefinition module)
        {
            if (module == null)
                return;

            LiveXamlPluginType = module.Types.FirstOrDefault(t => t.FullName == "__livexaml.LiveXamlPlugin");
        }


        public override void Rewrite()
        {
            //foreach (var clientStartMethod in ClientStartMethods) {
                InjectStartCall(_runtimeMembers);
            //}
        }

        private void InjectStartCall(RuntimeMembers runtimeMembers)
        {
            var moduleInitializer = _mainModule.GetOrCreateModuleInitializerMethod();
            var body = moduleInitializer.Body;
            var il = body.GetILProcessor();
            var firstInstruction = body.Instructions.LastOrDefault();
            var localIp = ServerIp ?? GetLocalIpAddress() ?? "127.0.0.1";

            if (firstInstruction == null) {
                firstInstruction = il.Create(OpCodes.Nop);
                body.Instructions.Add(firstInstruction);
            }
            
            foreach (var updateHandlerType in UpdateHandlerTypes) {
                var type = _mainModule.ImportReference(updateHandlerType);
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldtoken, type));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, runtimeMembers.GetTypeFromHandleMethod));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, runtimeMembers.RuntimeAddHandlerMethod));
            }
            
            foreach (var setting in Settings) {
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, setting.Key));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, setting.Value));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, runtimeMembers.RuntimeAddSettingMethod));
            }
            
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, localIp));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, _solutionPath));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, _projectName));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, _projectDir));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, _projectReferences));
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldstr, _nuGetPackagePath));

            if (LiveXamlPluginType != null)
            {   
                var liveXamlPluginType = _mainModule.ImportReference(LiveXamlPluginType);

                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldtoken, liveXamlPluginType));
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, runtimeMembers.GetTypeFromHandleMethod));
            }
            else
            {
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldnull));
            }

            if (TransportType != null)
            {   
                var transportType = _mainModule.ImportReference(TransportType);
                var transportConstructor = transportType.Resolve().GetConstructors().FirstOrDefault(c => c.Parameters.Count == 0);
                
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Newobj, _mainModule.ImportReference(transportConstructor)));
            }
            else
            {
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldnull));
            }

            if (DashboardType != null)
            {   
                var dashboardType = _mainModule.ImportReference(DashboardType);
                var dashboardConstructor = dashboardType.Resolve().GetConstructors().FirstOrDefault(c => c.Parameters.Count == 0);

                il.InsertBefore(firstInstruction, il.Create(OpCodes.Newobj, _mainModule.ImportReference(dashboardConstructor)));
            }
            else
            {
                il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldnull));
            }
            
            il.InsertBefore(firstInstruction, il.Create(OpCodes.Call, runtimeMembers.RuntimeStartMethod));
        }

        private string GetLocalIpAddress()
        {
            try {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0)) {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = (IPEndPoint)socket.LocalEndPoint;
                    return endPoint.Address.ToString();
                }
            } catch (Exception e) {
                _logger.LogWarning("Can't find local IP address");
                _logger.LogMessage(e.Message);
                return null;
            }
        }
        
        private bool IsClientStartMethod(MethodDefinition md)
        {
            if (StartRule != null)
                return StartRule.MatchesType(md.DeclaringType.FullName) &&
                       StartRule.MatchesMethod(md.Name) &&
                       StartRule.MatchesParameters(md.Parameters.Select(p => p.ParameterType.FullName).ToArray());
            
            return AutodetectStartMethod(md);
        }

        private bool AutodetectStartMethod(MethodDefinition md)
        {
            if (md.DeclaringType.Name == "Program" && md.Name == "Main")
                return md.Parameters.Count == 1 && md.Parameters[0].ParameterType.FullName == "System.String[]";

            if (md.DeclaringType.HasBaseType("Xamarin.Forms.Application"))
                return md.IsConstructor && !md.IsStatic;
            
            if (md.DeclaringType.HasBaseType("System.Windows.Application"))
                return md.IsConstructor && !md.IsStatic;
            
            if (md.DeclaringType.HasBaseType("Windows.UI.Xaml.Application"))
                return md.IsConstructor && !md.IsStatic;

            return false;
        }

        public override void ProcessMainModuleType(TypeDefinition type)
        {
            if (type.HasInterface("LiveSharp.ILiveSharpDashboard"))
                DashboardType = type;
            if (type.HasInterface("LiveSharp.Runtime.Network.ILiveSharpTransport")) {
                TransportType = _mainModule.ImportReference(TransportType).Resolve();;
            }

            if (type.HasInterface("LiveSharp.ILiveSharpUpdateHandler")) {
                _logger.LogMessage("Update handler: " + type);
                UpdateHandlerTypes.Add(_mainModule.ImportReference(type).Resolve());
            }

            foreach (var method in type.Methods) {
                if (IsClientStartMethod(method)) {
                    _logger.LogMessage("Client Start method detected: " + method.FullName);
                    ClientStartMethods.Add(method);
                }
            }
        }
    }
}