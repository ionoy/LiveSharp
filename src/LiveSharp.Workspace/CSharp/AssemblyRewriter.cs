using LiveSharp.Rewriters;
using LiveSharp.Rewriters.Serialization;
using LiveSharp.Shared.Parsing;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;

namespace LiveSharp.CSharp
{
    class AssemblyRewriter : IDisposable
    {
        private readonly Project _project;
        private readonly AssemblyContainer _assemblyContainer;
        private readonly AssemblyDiff _assemblyDiff;
        private readonly ILogger _logger;
        private readonly List<IDisposable> _stuffToDispose = new();

        public AssemblyRewriter(Project project, AssemblyContainer assemblyContainer, AssemblyDiff assemblyDiff, ILogger logger)
        {
            _project = project;
            _assemblyContainer = assemblyContainer;
            _assemblyDiff = assemblyDiff;
            _logger = logger;
        }

        public AssemblyRewriteResult RewriteUpdatedAssembly()
        {
            // if (!_assemblyDiff.HasIncompatibleUpdates)
            //     return new AssemblyRewriteResult.Ok();
            //
            
            return new AssemblyRewriteResult.Ok();
            
            //
            // while (_assemblyDiff.HasIncompatibleUpdates) {
            //     var updatedAssembly = assemblyContainer.AssemblyDefinitionOriginal;
            //     var updatedAssemblyMainModule = updatedAssembly.MainModule;
            //     var typesToExtract = diff.GetTypesToExtract();
            //
            //     // var typeExtractor = new TypeExtractor();
            //     // typeExtractor.ExtractTypes();
            // }
            //
            // var opts = StringComparison.InvariantCultureIgnoreCase;
            // var metadataReferences = _project.MetadataReferences;
            // var liveSharpRuntimeAssembly = metadataReferences.FirstOrDefault(r => Path.GetFileName((string?) r.Display) == "LiveSharp.Runtime.dll");
            //
            // if (liveSharpRuntimeAssembly == null) {
            //     _logger.LogError("Unable to rewrite the assembly. Can't find LiveSharp.Runtime.dll reference.");
            //     return;
            // }
            //
            // var supportAssemblies = metadataReferences
            //     .Where(r => Path.GetFileName(r.Display)?.StartsWith("LiveSharp.Support.", opts) == true)
            //     .ToArray();
            // var resolver = new InMemoryResolver();
            //
            // _stuffToDispose.Add(resolver);
            //
            // var assemblyDirs = supportAssemblies
            //     .Select(r => Path.GetDirectoryName(r.Display))
            //     .Distinct()
            //     .ToList();
            //
            // assemblyDirs.Add(Path.GetDirectoryName(liveSharpRuntimeAssembly?.Display));
            //
            // foreach (var assemblyDir in assemblyDirs)
            //     resolver.AddSearchDirectory(assemblyDir);
            //
            // var readerParameters = new ReaderParameters {
            //     AssemblyResolver = resolver,
            //     InMemory = true
            // };
            //
            // var runtimeAssemblyDefinition = AssemblyDefinition.ReadAssembly(liveSharpRuntimeAssembly.Display, readerParameters);
            // var supportAssemblyDefinitions = supportAssemblies
            //     .Select(sa => AssemblyDefinition.ReadAssembly(sa.Display, readerParameters))
            //     .ToArray();
            //
            // _stuffToDispose.Add(runtimeAssemblyDefinition);
            // _stuffToDispose.AddRange(supportAssemblyDefinitions);
            //
            // var rewriteLogger = new RewriteLogger(_logger.LogMessage, _logger.LogWarning, _logger.LogError, _logger.LogError);
            // var runtimeRewriter = new RuntimeRewriter(_assemblyContainer.AssemblyDefinitionForRewrite, runtimeAssemblyDefinition, supportAssemblyDefinitions, rewriteLogger);
            //
            //
            // // var oldName = _updatedAssembly.Name;
            // // var newVersion = new Version(oldName.Version.Major, oldName.Version.Minor, oldName.Version.Build, revision++);
            // //
            // // _updatedAssembly.Name.Version = newVersion;
            //     
            // runtimeRewriter.Rewrite();
        }

        public void Dispose()
        {
            foreach (var disposable in _stuffToDispose)
                disposable.Dispose();
        }
    }

    class AssemblyRewriteResult
    {
        internal class NeedAssemblyUpload : AssemblyRewriteResult
        {
            public IReadOnlyList<byte> AssemblyBuffer { get; }
            private static int _revisions = 0;

            public NeedAssemblyUpload(AssemblyDefinition assembly)
            {
                using var ms = new MemoryStream();
                using var symbolsMs = new MemoryStream();

                var oldVersion = assembly.Name.Version;
                
                assembly.Name.Version = new Version(oldVersion.Major, oldVersion.Minor, oldVersion.Build, ++_revisions);
                assembly.Write(ms, new WriterParameters {
                    WriteSymbols = true, 
                    SymbolStream = symbolsMs, 
                    SymbolWriterProvider = new PortablePdbWriterProvider()
                });

                var buffer = new List<byte>();
                var assemblyUpdate = new LiveSharpAssemblyUpdate(assembly.Name.Name, ms.ToArray(), symbolsMs.ToArray());
                
                new ObjectParser<LiveSharpAssemblyUpdate>().Serialize(assemblyUpdate, buffer);
                
                AssemblyBuffer = buffer;
            }
        }

        internal class Ok : AssemblyRewriteResult {}
    }
    
    
}