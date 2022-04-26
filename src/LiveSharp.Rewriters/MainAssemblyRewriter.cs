using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using Mono.Cecil.Rocks;
using System.Diagnostics;

namespace LiveSharp.Rewriters
{
    public class MainAssemblyRewriter
    {
        public string AssemblyPath { get; }
        public string References { get; }
        public bool AlreadyInjected { get; private set; }
        
        private readonly RewriteLogger _log;
        private readonly bool _injectStart;

        private ISymbolReaderProvider _debugReaderProvider;
        private ISymbolWriterProvider _debugWriterProvider;
        private string _pdbFilename;
        private bool _pdbFound;
        private bool _mdbFound;
        private string _mdbFilename;

        public MainAssemblyRewriter(string assemblyPath, string references,
            RewriteLogger log, bool injectStart = true)
        {
            AssemblyPath = assemblyPath;
            References = references;
            
            _log = log;
            _injectStart = injectStart;
        }

        public bool ProcessEverything(
            string projectDir, 
            string solutionPath, 
            string projectName, 
            string nuGetPackagePath, 
            string projectReferences, 
            out bool isNonMsBuildConfiguration, 
            bool isFullRebuild = true)
        {
            //Debugger.Launch();
            GetSymbolProviders();

            isNonMsBuildConfiguration = false;
            
            var tempFileName = AssemblyPath + ".ls";
            var useOriginalAssembly = !isFullRebuild;
            var originalAssemblyFilename = AssemblyPath + ".org";
            var assemblyPath = useOriginalAssembly && File.Exists(originalAssemblyFilename) ? originalAssemblyFilename : AssemblyPath;

            if (isFullRebuild)
                File.Copy(AssemblyPath, originalAssemblyFilename, true);

            File.Copy(assemblyPath, tempFileName, true);

            using var resolver = new DefaultAssemblyResolver();
            var references = References.Split(';').ToArray();
            var liveSharpRuntimeReference = references.FirstOrDefault(r => r.EndsWith("LiveSharp.Runtime.dll", StringComparison.InvariantCultureIgnoreCase) || r.EndsWith("LiveSharp.Runtime.BlazorWasm.dll", StringComparison.InvariantCultureIgnoreCase));
            var liveXamlStandardReference = references.FirstOrDefault(r => r.EndsWith("LiveXaml.Standard.dll", StringComparison.InvariantCultureIgnoreCase));
            var supportReferences = references.Where(IsSupportReference).ToArray();
            
            if (liveSharpRuntimeReference == null)
            {
                _log.LogMessage("LiveSharp.Runtime.dll wasn't referenced");
                
                isNonMsBuildConfiguration = true;
                return true;
            }

            foreach (var liveSharpSupportReference in supportReferences) {
                if (liveSharpSupportReference?.EndsWith("LiveSharp.Support.BlazorWASM.dll", StringComparison.InvariantCultureIgnoreCase) == true) {
                    var signalRClient = references.FirstOrDefault(r => r.EndsWith("Microsoft.AspNetCore.SignalR.Client.dll",
                        StringComparison.InvariantCultureIgnoreCase));
                    if (signalRClient == null) {
                        _log.LogError("Please install 'Microsoft.AspNetCore.SignalR.Client' NuGet package to use LiveSharp with Blazor Web Assembly");
                        return false;
                    }
                }
            }

            var assemblyDirs = references.Select(Path.GetDirectoryName).Distinct();

            foreach (var assemblyDir in assemblyDirs)
                resolver.AddSearchDirectory(assemblyDir);

            try {
                var supportReferenceModules = supportReferences
                    .Where(r => r != null)
                    .Select(supportReference => ModuleDefinition.ReadModule(supportReference, new ReaderParameters {
                        AssemblyResolver = resolver,
                        InMemory = true
                    }))
                    .ToArray();
                
                using var liveSharpRuntimeModule = ModuleDefinition.ReadModule(liveSharpRuntimeReference, new ReaderParameters {
                    AssemblyResolver = resolver
                });
                using var mainModule = ReadModule(resolver, tempFileName);
                
                if (mainModule.Types.Any(type => type.Name == "<liveSharpInjected>")) {
                    AlreadyInjected = true;
                    return true;
                }

                mainModule.Types.Add(new TypeDefinition(null, "<liveSharpInjected>", TypeAttributes.NotPublic, mainModule.ImportReference(mainModule.TypeSystem.Object)));

                var runtimeMembers = RuntimeMembers.FromRuntimeAssembly(liveSharpRuntimeModule, mainModule, _log);
                var updateHookRewriter = new UpdateHookRewriter(_log, mainModule, runtimeMembers);
                var startRewriter = new LiveSharpStartRewriter(_log, mainModule, runtimeMembers, nuGetPackagePath, projectDir, projectName, solutionPath, projectReferences);
                var interceptorProcessor = new IlRewritersProcessor(mainModule);
                var inpcRewriter = new InpcRewriter(runtimeMembers);
                //var blazorDiRewriter = new BlazorDiReplaceRewriter(runtimeMembers, mainModule, _log);
                ModuleDefinition liveXamlModule = null;
                
                if (liveXamlStandardReference != null) {
                    liveXamlModule = ModuleDefinition.ReadModule(liveXamlStandardReference, new ReaderParameters { 
                        AssemblyResolver = resolver,
                        InMemory = true
                    });
                    startRewriter.ProcessLiveXamlModule(liveXamlModule);
                }

                AttributeLoader.LoadBuildAttributes(mainModule, startRewriter, updateHookRewriter);
                    
                var allTypes = mainModule.GetAllTypes();
                foreach (var type in allTypes) {
                    updateHookRewriter.ProcessMainModuleType(type);
                    startRewriter.ProcessMainModuleType(type);
                    interceptorProcessor.ProcessMainModuleType(type);
                    inpcRewriter.ProcessMainModuleType(type);
                  //  blazorDiRewriter.ProcessMainModuleType(type);
                }
                
                foreach (var supportReferenceModule in supportReferenceModules) {
                    updateHookRewriter.ProcessSupportModule(supportReferenceModule);
                    startRewriter.ProcessSupportModule(supportReferenceModule);
                    interceptorProcessor.ProcessSupportModule(supportReferenceModule);
                    inpcRewriter.ProcessSupportModule(supportReferenceModule);
                    //blazorDiRewriter.ProcessSupportModule(supportReferenceModule);
                }

                updateHookRewriter.Rewrite();
                interceptorProcessor.Rewrite();
                //blazorDiRewriter.Rewrite();
                    
                if (_injectStart && !startRewriter.SkipStart) {
                    // if (startRewriter.ClientStartMethods.Count == 0) {
                    //     _log.LogWarning(
                    //         $"LiveSharp couldn't find Start method.{Environment.NewLine}" +
                    //         "If this is the main assembly, open `LiveSharp.dashboard.cs` file and uncomment the [LiveSharpStart] attribute");
                    // } else {
                    //     startRewriter.Rewrite();
                    // }
                    startRewriter.Rewrite();
                }
                
                inpcRewriter.Rewrite();

                var parameters = new WriterParameters
                {
                    WriteSymbols = true,
                    SymbolWriterProvider = _debugWriterProvider
                };

                mainModule.Write(AssemblyPath, parameters);

                foreach (var supportReferenceModule in supportReferenceModules) {
                    supportReferenceModule.Dispose();
                }

                liveXamlModule?.Dispose();
            }
            catch (Exception e)
            {
                _log.LogError("LiveSharp exception: " + e);
                return false;
            }

            return true;
        }
        private static bool IsSupportReference(string r)
        {
            var fileName = Path.GetFileName(r);
            
            return fileName.StartsWith("LiveSharp.Support.", StringComparison.InvariantCultureIgnoreCase) ||
                   fileName.Equals("LiveBlazor.dll", StringComparison.InvariantCultureIgnoreCase) ||
                   fileName.Equals("LiveBlazor.WASM.dll", StringComparison.InvariantCultureIgnoreCase);
        }


        private void GetSymbolProviders()
        {
            _pdbFilename = Path.ChangeExtension(AssemblyPath, "pdb");
            _mdbFilename = AssemblyPath + ".mdb";

            _pdbFound = false;
            _mdbFound = false;

            _pdbFound = File.Exists(_pdbFilename);
            _mdbFound = File.Exists(_mdbFilename);

            if (_pdbFound) {
                _debugReaderProvider = new PdbReaderProvider();
                _debugWriterProvider = new PdbWriterProvider();
            } else if (_mdbFound) {
                _debugReaderProvider = new MdbReaderProvider();
                _debugWriterProvider = new MdbWriterProvider();
            }
        }

        private ModuleDefinition ReadModule(DefaultAssemblyResolver resolver, string assemblyPath)
        {
            if (_pdbFound) {
                using (var symbolStream = File.OpenRead(_pdbFilename)) {
                    var rp = new ReaderParameters {
                        AssemblyResolver = resolver,
                        ReadSymbols = (_pdbFound || _mdbFound),
                        SymbolReaderProvider = _debugReaderProvider,
                        SymbolStream = symbolStream
                    };
                    return ModuleDefinition.ReadModule(assemblyPath, rp);
                }
            }
            var rp2 = new ReaderParameters {
                AssemblyResolver = resolver,
                ReadSymbols = (_pdbFound || _mdbFound),
                SymbolReaderProvider = _debugReaderProvider
            };
            return ModuleDefinition.ReadModule(assemblyPath, rp2);
        }
    }
}