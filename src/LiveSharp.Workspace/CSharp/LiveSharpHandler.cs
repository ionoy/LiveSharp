using LiveSharp.Infrastructure;
using LiveSharp.Rewriters;
using LiveSharp.Rewriters.Serialization;
using LiveSharp.Runtime;
using LiveSharp.ServerClient;
using LiveSharp.Shared.Network;
using LiveSharp.VisualStudio.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using AssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using Task = System.Threading.Tasks.Task;
using TypeDefinition = Mono.Cecil.TypeDefinition;

namespace LiveSharp.CSharp
{
    public class LiveSharpHandler
    {
        private readonly ILogger _logger;
        private readonly LiveSharpWorkspace _workspace;
        private readonly TypeRegistry _typeRegistry;
        private readonly string _cachePath;

        private readonly ConcurrentDictionary<string, DateTime> _changeTimeMap = new();
        private readonly ConcurrentDictionary<string, AssemblyContainer> _previousDisposableAssemblies = new();

        public LiveSharpHandler(LiveSharpWorkspace workspace, ILogger logger)
        {
            _logger = logger;
            _workspace = workspace;
            _typeRegistry = new TypeRegistry(new RewriteLogger(logger.LogMessage, logger.LogWarning, logger.LogError, logger.LogError));
            _cachePath = Path.Combine(Path.GetTempPath(), "LiveSharp", "Cache");

            try {
                if (!Directory.Exists(_cachePath))
                    Directory.CreateDirectory(_cachePath);
            }
            catch (Exception e) {
                _logger.LogError("Couldn't create a cache directory", e);
            }
        }

        public void SetMainProjectName(string projectName)
        {}

        public async Task FileChanged(TextDocument textDocument, Solution solution)
        {
            _logger.LogDebug($"File changed: {textDocument.Name}");
            
            if (_changeTimeMap.TryGetValue(textDocument.FilePath, out var lastUpdateTime))
                if (DateTime.Now - lastUpdateTime < TimeSpan.FromMilliseconds(100))
                    return;

            _changeTimeMap[textDocument.FilePath] = DateTime.Now;

            var (success, source) = await FSUtils.ReadFileTextAsync(textDocument.FilePath);
            if (!success) {
                _logger.LogError("Couldn't read the updated file");
                return;
            }

            await SourceChanged(solution, source, textDocument.Id);
        }

        public async Task SourceChanged(Solution solution, string source, DocumentId documentId, bool isDryRun = false, bool applyWorkspaceChanges = true)
        {
            solution = solution.WithDocumentText(documentId, SourceText.From(source, Encoding.Unicode));
            
            if (applyWorkspaceChanges)
                solution.Workspace.TryApplyChanges(solution);

            var doc = solution.GetDocument(documentId);
            var semanticModel = await doc.GetSemanticModelAsync().ConfigureAwait(false);
           
            
            PrepareUpdatesAsync(doc.Project, semanticModel, doc.Name, isDryRun);
        }

        public void PrepareUpdatesAsync(Project project, SemanticModel semanticModel, string documentName, bool isDryRun)
        {
            try {
                // var generators = project.AnalyzerReferences.SelectMany(r => r.GetGeneratorsForAllLanguages());
                // var additionalTexts = project
                //     .AdditionalDocuments
                //     .Where(d => d.Name.EndsWith("razor", StringComparison.InvariantCultureIgnoreCase))
                //     .Select(d => new LiveSharpAdditionalText(d));
                // var driver = CSharpGeneratorDriver.Create(generators, optionsProvider: project.AnalyzerOptions.AnalyzerConfigOptionsProvider, additionalTexts: additionalTexts);
                //
                // driver.RunGeneratorsAndUpdateCompilation(semanticModel.Compilation, out var generatedCompilation, out var diagnostics2);
                var diagnostics = semanticModel.Compilation.GetDiagnostics();
                //var diagnostics = semanticModel.GetDiagnostics();
                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                
                if (errors.Length > 0) {
                    foreach (var error in errors) {
                        _logger.LogWarning(error.ToString());
                    }

                    return;
                }

                var disposableAssembly = CreateDynamicAssembly(semanticModel.Compilation, project, _logger);

                if (!isDryRun) {
                    MainUpdate(project, documentName, disposableAssembly);
                }
                else {
                    DryRun(project, disposableAssembly, disposableAssembly);
                }

                if (_previousDisposableAssemblies.TryGetValue(project.Name, out var oldDisposableAssembly))
                    oldDisposableAssembly.Dispose();

                _previousDisposableAssemblies[project.Name] = disposableAssembly;
            }
            catch (Exception ex) {
                _logger.LogError("FileSaved handler failed" + Environment.NewLine + ex);
            }
        }

        private void MainUpdate(Project project, string documentName, AssemblyContainer assemblyContainer)
        {
            if (_previousDisposableAssemblies.TryGetValue(project.Name, out var previousAssemblyContainer)) {
                _logger.LogMessage("Updating " + documentName);

                var processor = new AssemblyUpdateProcessor(previousAssemblyContainer.AssemblyDefinitionOriginal, assemblyContainer.AssemblyDefinitionOriginal,
                    _logger);
                var diff = processor.CreateDiff();

                if (diff.HasUpdates()) {
                    _logger.LogDebug("Diff has updates");
                    var documentSerializer = new DocumentSerializer(diff, _typeRegistry, assemblyContainer.AssemblyDefinitionOriginal.FullName);
                    var documentElement = documentSerializer.Serialize();

                    using var rewriter = new AssemblyRewriter(project, assemblyContainer, diff, _logger);
                    var rewriteResult = rewriter.RewriteUpdatedAssembly();

                    if (rewriteResult is AssemblyRewriteResult.Ok) {
                        SendDocumentElement(documentElement);
                    } else if (rewriteResult is AssemblyRewriteResult.NeedAssemblyUpload assemblyUpload) {
                        SendDocumentWithAssembly(assemblyUpload.AssemblyBuffer, documentElement);
                    }
                } else {
                    _logger.LogDebug("Diff doesn't have any updates");
                }
            }
            else {
                _logger.LogWarning("Previous compilation is missing, try updating the code again");
            }
        }

        private void DryRun(Project project, AssemblyContainer assemblyContainerForCache, AssemblyContainer assemblyContainerForSending)
        {
            var timestampDocument = project.AdditionalDocuments.FirstOrDefault(d => d.Name.EndsWith(".timestamp"));
            if (timestampDocument != null) {
                var cacheFileName = Path.GetFileNameWithoutExtension(timestampDocument.Name) + ".cache";
                var cacheFiles = Directory.EnumerateFiles(_cachePath, "*.cache");
                var currentCacheFile = cacheFiles.FirstOrDefault(cacheFile => Path.GetFileName(cacheFile) == cacheFileName);

                if (currentCacheFile == null) {
                    WriteInitialCacheFile(assemblyContainerForCache.AssemblyDefinitionOriginal, cacheFileName);
                }
                else {
                    using var cacheAssembly = LoadCacheAssembly(currentCacheFile, project);

                    var processor = new AssemblyUpdateProcessor(cacheAssembly, assemblyContainerForSending.AssemblyDefinitionOriginal, _logger);
                    var diff = processor.CreateDiff();

                    if (diff.HasUpdates()) {
                        _logger.LogMessage($"Project {project.Name} is out of date. Sending diff.");

                        // if (diff.HasIncompatibleUpdates && false) {
                        //     // using var rewriter = new AssemblyRewriter(assemblyContainerForSending, _logger);
                        //     // rewriter.RewriteUpdatedAssembly(project);
                        //     // SendDocumentWithAssembly(assemblyContainerForSending.AssemblyDefinitionForRewrite);
                        // }
                        //else {
                            var documentSerializer =
                                new DocumentSerializer(diff, _typeRegistry, assemblyContainerForSending.AssemblyDefinitionOriginal.FullName);
                            var documentElement = documentSerializer.Serialize();
                            SendDocumentElement(documentElement);
                        //}
                    }
                }
            }
        }

        public static AssemblyContainer CreateDynamicAssembly(Compilation compilation, Project project, ILogger logger)
        {
            var memoryStream = new MemoryStream();
            var pdbMemoryStream = new MemoryStream();

            // compilation = compilation
            //     .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            //     .WithAllowUnsafe(true));

            var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
            var result = compilation.Emit(memoryStream, pdbMemoryStream, options: emitOptions);

            memoryStream.Position = 0;
            pdbMemoryStream.Position = 0;

            foreach (var diagnostic in result.Diagnostics) {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    logger.LogMessage(diagnostic.GetMessage());
            }

            if (!result.Success)
                throw new Exception("Serializing document failed");

            var resolver = new InMemoryResolver();

            var references = project.MetadataReferences.Select(r => r.Display);
            var assemblyDirs = references.Select(Path.GetDirectoryName).Distinct();

            foreach (var assemblyDir in assemblyDirs)
                resolver.AddSearchDirectory(assemblyDir);

            return new AssemblyContainer(memoryStream, pdbMemoryStream, resolver);
        }

        private AssemblyDefinition LoadCacheAssembly(string assemblyPath, Project project)
        {
            var resolver = new InMemoryResolver();
            var references = project.MetadataReferences.Select(r => r.Display);
            var assemblyDirs = references.Select(Path.GetDirectoryName).Distinct();

            foreach (var assemblyDir in assemblyDirs)
                resolver.AddSearchDirectory(assemblyDir);

            var readerParameters =
                new ReaderParameters {ReadSymbols = true, SymbolReaderProvider = new PortablePdbReaderProvider(), AssemblyResolver = resolver};

            return AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
        }

        private void WriteInitialCacheFile(AssemblyDefinition assembly, string cacheFileName)
        {
            DeleteOldCacheFiles();

            var writerParameters = new WriterParameters {WriteSymbols = true, SymbolWriterProvider = new PortablePdbWriterProvider()};
            assembly.Write(Path.Combine(_cachePath, cacheFileName), writerParameters);
            //assembly.Write(Path.Combine(_cachePath, cacheFileName));
        }

        private void DeleteOldCacheFiles()
        {
            foreach (var oldCache in Directory.EnumerateFiles(_cachePath, "*.cache")) {
                for (int i = 0; i < 3; i++) {
                    try {
                        if (File.Exists(oldCache))
                            File.Delete(oldCache);

                        var pdbFilename = Path.ChangeExtension(oldCache, ".pdb");

                        if (File.Exists(pdbFilename))
                            File.Delete(pdbFilename);
                    }
                    catch {
                        _logger.LogWarning("Cannot delete " + oldCache + ". Retrying in a second...");
                        Thread.Sleep(1000);
                    }
                }
            }
        }

        private void SendDocumentWithAssembly(IReadOnlyList<byte> assemblyBuffer, XElement document)
        {
            var assemblyMessage = new ServerMessage(assemblyBuffer.ToArray(), ContentTypes.LiveSharp.AssemblyUpdate, MessageType.Broadcast, BroadcastGroups.LiveSharp);
            var content = document.ToString(SaveOptions.DisableFormatting);
            var documentMessage = new ServerMessage(Encoding.Unicode.GetBytes(content), ContentTypes.LiveSharp.DocumentElement, MessageType.Broadcast, BroadcastGroups.LiveSharp);

            var buffer = new[] {assemblyMessage, documentMessage}.SelectMany(m => m.CreateBuffer()).ToArray();
            
            _workspace.SendBroadcast(buffer, ContentTypes.Multipart, BroadcastGroups.LiveSharp);
        }

        private void SendDocumentElement(XElement document)
        {
            var content = document.ToString(SaveOptions.DisableFormatting);
            var buffer = Encoding.Unicode.GetBytes(content);
            
            _workspace.SendBroadcast(buffer, ContentTypes.LiveSharp.DocumentElement, BroadcastGroups.LiveSharp);
        }
    }

    public class TypeExtractor
    {
        private readonly AssemblyDefinition _assembly;
        private static int _assemblyCount;

        public TypeExtractor(AssemblyDefinition assembly)
        {
            _assembly = assembly;
        }

        public void ExtractTypes(TypeDefinition[] typesToExtract)
        {
            var assemblyMainModule = _assembly.MainModule;

            if (typesToExtract.Length > 0) {
                var assemblyName = _assembly.Name;
                assemblyName.Name += " " + _assemblyCount++;

                var newAssembly = AssemblyDefinition.CreateAssembly(assemblyName, assemblyName.Name, ModuleKind.Dll);
                var newAssemblyMainModule = newAssembly.MainModule;

                foreach (var assemblyReference in assemblyMainModule.AssemblyReferences)
                    newAssemblyMainModule.AssemblyReferences.Add(assemblyReference);

                var extractedTypes = new List<TypeDefinition>();

                foreach (var typeToExtract in typesToExtract) {
                    var extractedType = new TypeDefinition(typeToExtract.Namespace, typeToExtract.Name, typeToExtract.Attributes, typeToExtract.BaseType);
                    extractedTypes.Add(extractedType);
                    newAssemblyMainModule.Types.Add(extractedType);
                }

                var allTypes = _assembly.GetAllTypes();
            }
        }
    }

}