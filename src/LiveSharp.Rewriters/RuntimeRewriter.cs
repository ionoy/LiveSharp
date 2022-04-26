using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace LiveSharp.Rewriters
{
    public class RuntimeRewriter
    {
        private readonly AssemblyDefinition _updatedAssembly;
        private readonly AssemblyDefinition _liveSharpRuntimeAssembly;
        private readonly AssemblyDefinition[] _supportAssemblies;
        private readonly RewriteLogger _logger;

        public RuntimeRewriter(AssemblyDefinition updatedAssembly, AssemblyDefinition liveSharpRuntimeAssembly, AssemblyDefinition[] supportAssemblies, RewriteLogger logger)
        {
            _updatedAssembly = updatedAssembly;
            _liveSharpRuntimeAssembly = liveSharpRuntimeAssembly;
            _supportAssemblies = supportAssemblies;
            _logger = logger;
        }

        public void Rewrite()
        {
            var liveSharpRuntimeModule = _liveSharpRuntimeAssembly.MainModule;
            var mainModule = _updatedAssembly.MainModule;
            
            var runtimeMembers = RuntimeMembers.FromRuntimeAssembly(liveSharpRuntimeModule, mainModule, _logger);
            var updateHookRewriter = new UpdateHookRewriter(_logger, mainModule, runtimeMembers);
            var interceptorProcessor = new IlRewritersProcessor(mainModule);
            var inpcRewriter = new InpcRewriter(runtimeMembers);
            var blazorDiRewriter = new BlazorDiReplaceRewriter(runtimeMembers, mainModule, _logger);
            
            AttributeLoader.LoadBuildAttributes(mainModule, null, updateHookRewriter);
            
            var allTypes = mainModule.GetAllTypes();
            foreach (var type in allTypes) {
                updateHookRewriter.ProcessMainModuleType(type);
                interceptorProcessor.ProcessMainModuleType(type);
                inpcRewriter.ProcessMainModuleType(type);
                blazorDiRewriter.ProcessMainModuleType(type);
            }
            
            foreach (var supportReferenceModule in _supportAssemblies) {
                updateHookRewriter.ProcessSupportModule(supportReferenceModule.MainModule);
                interceptorProcessor.ProcessSupportModule(supportReferenceModule.MainModule);
                inpcRewriter.ProcessSupportModule(supportReferenceModule.MainModule);
                blazorDiRewriter.ProcessSupportModule(supportReferenceModule.MainModule);
            }

            updateHookRewriter.Rewrite();
            interceptorProcessor.Rewrite();
            inpcRewriter.Rewrite();
            blazorDiRewriter.Rewrite();
        }
    }
}