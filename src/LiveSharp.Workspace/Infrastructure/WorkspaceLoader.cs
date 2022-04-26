using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;

namespace LiveSharp.Infrastructure
{
    public class WorkspaceLoader
    {
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, string>> ProjectProperties { get; } = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>();
        
        public static async Task<Workspace> GetWorkspace(
            string nugetPath, 
            string projectDir, 
            string projectName,
            ConcurrentDictionary<string, string> additionalSettings, 
            ILogger logger)
        {
            var solutionCachePath = Path.Combine(nugetPath, "SolutionCache");

            if (!Directory.Exists(solutionCachePath))
                throw new Exception($"SolutionCache directory doesn't exist: {solutionCachePath}");

            var projectList = Path.Combine(solutionCachePath, "project.list");
            var projectLines = await FSUtils.ReadFileLinesAsync(projectList);
            var projects = projectLines.Select(line => line.Split(new[] { '=' }, 2))
                                         .Where(split => split.Length == 2)
                                         .ToDictionary(split => split[0], split => split[1]);
            var workspace = new AdhocWorkspace();
            
            await LoadProject(workspace, projectDir, projectName, additionalSettings, projects, solutionCachePath, logger);

            if (logger.IsDebugLoggingEnabled) {
                foreach (var project in workspace.CurrentSolution.Projects) {
                    logger.LogDebug("Loaded project " + project.Name);
                    logger.LogDebug("Documents: " + string.Join(",", project.Documents.Select(d => d.Name)));
                    logger.LogDebug("AdditionalDocuments: " + string.Join(",", project.AdditionalDocuments.Select(d => d.Name)));
                }
            }
            
            return workspace;
        }
        private static async Task LoadProject(AdhocWorkspace workspace, string loadingProjectDir, string loadingProjectName, ConcurrentDictionary<string, string> additionalSettings, Dictionary<string, string> projects, string solutionCachePath, ILogger logger)
        {
            logger.LogMessage($"Loading project {loadingProjectName}");
            
            string projectInfoDirName = null;

            foreach (var project in projects) {
                var projectId = project.Key;
                var projectIdSplit = projectId.Split('>');
                
                if (projectIdSplit.Length != 2)
                    continue;

                var projectDir = projectIdSplit[0];
                var projectName = projectIdSplit[1];
                
                if (!string.Equals(loadingProjectName, projectName, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (normalizePath(loadingProjectDir) == normalizePath(projectDir))
                    projectInfoDirName = project.Value;
            }

            if (projectInfoDirName == null) {
                logger.LogMessage($"LiveSharp not installed in {loadingProjectName}");
                return;
            }

            var projectInfoDir = Path.Combine(solutionCachePath, projectInfoDirName);

            if (!Directory.Exists(projectInfoDir)) {
                logger.LogError("Project info directory doesn't exist for " + projectInfoDirName);
                return;
            }

            var projectInfoFiles = Directory.GetFiles(projectInfoDir, "*.info");
            var projectReferences = new List<(string projectDir, string projectName)>();

            foreach (var projectInfoPath in projectInfoFiles) {
                if (!File.Exists(projectInfoPath)) {
                    logger.LogError("Couldn't find project info at " + projectInfoPath);
                    return;
                }

                var projectInfoLines = await FSUtils.ReadFileLinesAsync(projectInfoPath);
                var projectInfoDictionary = projectInfoLines
                    .Select(line => line.Split(new[] {'='}, 2))
                    .Where(split => split.Length == 2)
                    .ToDictionary(split => split[0], split => split[1]);

                projectReferences.AddRange(GetProjectReferences(loadingProjectDir, projectInfoDictionary["ProjectReferences"]));
                
                var projectInfo = GetProjectInfo(projectInfoDir, loadingProjectName, projectInfoDictionary, additionalSettings);

                if (Directory.Exists(projectInfo.FilePath)) {
                    workspace.AddProject(projectInfo);
                } else {
                    // Project was probably removed from solution, fix solution cache
                    File.Delete(projectInfoPath);
                }
            }

            foreach (var projectReference in projectReferences) {
                await LoadProject(workspace, projectReference.projectDir, projectReference.projectName, additionalSettings, projects, solutionCachePath, logger);
            }
            
            string normalizePath(string path) =>
                Path.GetFullPath(new Uri(path).LocalPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToUpperInvariant();
        }
        
        private static IEnumerable<(string projectDir, string projectName)> GetProjectReferences(string rootProjectDir, string projectReferences)
        {
            if (string.IsNullOrWhiteSpace(projectReferences))
                return Enumerable.Empty<(string projectDir, string projectName)>();
            
            return projectReferences
                .Split(';')
                .Select(projectFilePath => {
                    var projectName = Path.GetFileNameWithoutExtension(projectFilePath);
                    var projectDir = Path.Combine(rootProjectDir, Path.GetDirectoryName(projectFilePath));

                    return (projectDir, projectName);
                });
        }

        private static ProjectInfo GetProjectInfo(string solutionCacheDir, string projectName, Dictionary<string, string> properties, ConcurrentDictionary<string, string> additionalSettings)
        {
            var projectId = ProjectId.CreateNewId();
            var sources = properties["Sources"].Split(';');
            var references = properties["References"].Split(';');
            var projectDir = properties["ProjectDir"];
            var assemblyName = properties["AssemblyName"];
            var defineConstants = properties["DefineConstants"];
            var assemblyPath = properties["AssemblyPath"];
            var rootNamespace = properties["RootNamespace"];
            var outputType = properties["OutputType"];
            
            //var langVersion = properties["LangVersion"];
            //var langVersion = "8.0";
            var embeddedResource = properties["EmbeddedResource"].Split(';');
            var razorFiles = new string[0];
            
            if (properties.TryGetValue("RazorComponentWithTargetPath", out var razorFilesLine))
                razorFiles = razorFilesLine.Split(';');

            for (int i = 0; i < razorFiles.Length; i++) {
                var razorFile = razorFiles[i];
                var razorFileSplit = razorFile.Split(",");

                if (razorFileSplit.Length == 2) {
                    razorFiles[i] = razorFileSplit[0];
                    additionalSettings[razorFileSplit[0]] = razorFileSplit[1];
                }
            }

            var contentFiles = new string[0];
            if (properties.TryGetValue("Content", out var content)) 
                contentFiles = content.Split(';');

            var parseOptions = CreateParseOptions(defineConstants);

            if (ProjectProperties.TryGetValue(projectName, out var props)) {
                props["RootNamespace"] = rootNamespace;
            } else {
                ProjectProperties[projectName] = new ConcurrentDictionary<string, string> {
                    ["RootNamespace"] = rootNamespace 
                };
            }
            
            var analyzerConfigFilePaths = Array.Empty<string>();
            if (properties.TryGetValue("AnalyzerConfigFiles", out var analyzerConfigFilePathsLine)) {
                var currentDirectory = Path.GetDirectoryName(typeof(WorkspaceLoader).Assembly.Location);
                var razorConfigFile = Path.Combine(currentDirectory, "RazorSourceGenerator.razorencconfig");
                
                analyzerConfigFilePaths = analyzerConfigFilePathsLine
                    .Split(';')
                    .Append(razorConfigFile).ToArray();
            }

            var defaultAnalyzerAssemblyLoader = typeof(AnalyzerFileReference)
                .Assembly
                .GetType("Microsoft.CodeAnalysis.DefaultAnalyzerAssemblyLoader");
            var analyzerAssemblyLoader = defaultAnalyzerAssemblyLoader.GetConstructors().FirstOrDefault().Invoke(new object[0]);
            var analyzerFileReferenceCtor = typeof(AnalyzerFileReference).GetConstructors().FirstOrDefault();
            
            var analyzerReferences = Array.Empty<AnalyzerFileReference>();
            if (properties.TryGetValue("Analyzers", out var analyzerReferenceLines) && !string.IsNullOrWhiteSpace(analyzerReferenceLines))
                analyzerReferences = analyzerReferenceLines
                    .Split(';')
                    //.Select(SubstituteRazorSourceGeneratorAssemblies)
                    .Select(l => (AnalyzerFileReference)analyzerFileReferenceCtor.Invoke(new[] { l, analyzerAssemblyLoader }))
                    .ToArray();
            
            var documents = GetDocuments(sources, projectDir, projectId);
            var contentFilesWithoutRazor = contentFiles.Where(cf => !razorFiles.Contains(cf));
            var additionalDocumentPaths = embeddedResource
                .Concat(razorFiles)
                .Concat(contentFilesWithoutRazor);
            
            var additionalDocuments = GetDocuments(additionalDocumentPaths, projectDir, projectId).ToArray();
            var analyzerConfigFiles = GetDocuments(analyzerConfigFilePaths, projectDir, projectId).ToArray();

            ImmutableArray<byte> publicKeyBuffer = ImmutableArray<byte>.Empty;

            if (projectName == "LiveBlazor.Dashboard") {
                var publicKey = "0024000004800000140100000602000000240000525341310008000001000100b72da9a756f5789d8573eda75ed086b1257ff762852ed92cf3716c2a93fd52f4a83bc3186ce57cdd484c3dedd304442c10773bb21445766b3301b53c5bbe9d157fed1ff1fb0d4c7e2a8ff6e0c8ad43b524f42fece8cf669808f6471ae0d962ba6fc752b990b8c172cf7df45b81ce2377ecea50b5fe3f48787475a30bd364fd7b350c2230f37880e503a82f960bfad2f7f92032d128b1ff1d151519f0c66ad93b006dd2e43add8a0adfa82346c150802a4ccca45f0af0785418b2160153907313f346ea9a22dafeed41789442263b49890e33dc7a7a5c8b4f0772e587a6b40b202202a4cab6bbba8e520fddf0d6d74d4f4a6da8916f682b45acda4cb7778297b1";
                publicKeyBuffer = hexStringToByteArray(publicKey);
                
                ImmutableArray<byte> hexStringToByteArray(string hexString)
                {
                    MemoryStream stream = new MemoryStream(hexString.Length / 2);

                    for (int i = 0; i < hexString.Length; i += 2)
                        stream.WriteByte(byte.Parse(hexString.Substring(i, 2), System.Globalization.NumberStyles.AllowHexSpecifier));
                    
                    return stream.ToArray().ToImmutableArray();
                }
            }
            
            var outputKind = outputType == "Library" ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication;
            
            return ProjectInfo.Create(projectId,
                VersionStamp.Create(),
                projectName,
                assemblyName,
                LanguageNames.CSharp,
                documents: documents,
                additionalDocuments: AppendTimestampDocument(solutionCacheDir, properties, projectId,
                    additionalDocuments),
                parseOptions: parseOptions,
                metadataReferences: GetMetadataReferences(references),
                filePath: projectDir,
                outputFilePath: assemblyPath
            ).WithAnalyzerReferences(analyzerReferences)
            .WithAnalyzerConfigDocuments(analyzerConfigFiles)
            .WithCompilationOptions(new CSharpCompilationOptions(outputKind, allowUnsafe: true, cryptoPublicKey: publicKeyBuffer, publicSign: publicKeyBuffer.Length > 0));
        }
        
        private static string SubstituteRazorSourceGeneratorAssemblies(string analyzerReference)
        {
            var assembliesToSubstitute = new[] { "Microsoft.AspNetCore.Razor.Language.dll", "Microsoft.NET.Sdk.Razor.SourceGenerators.dll", "Microsoft.AspNetCore.Mvc.Razor.Extensions.dll", "Microsoft.CodeAnalysis.Razor.dll" };
            var analyzerFilename = Path.GetFileName(analyzerReference);
            
            if (assembliesToSubstitute.Any(assembly => string.Equals(assembly, analyzerFilename, StringComparison.InvariantCultureIgnoreCase))) {
                var directoryName = Path.GetDirectoryName(typeof(WorkspaceLoader).Assembly.Location);
                var currentDirectory = directoryName;
                var newPath = Path.Combine(currentDirectory, analyzerFilename);
                return newPath;
            }
            
            return analyzerReference;
        }

        private static IEnumerable<DocumentInfo> AppendTimestampDocument(string solutionCacheDir,
            Dictionary<string, string> properties, ProjectId projectId, IReadOnlyList<DocumentInfo> additionalDocuments)
        {
            try {
                var modifiedDocuments = additionalDocuments as IEnumerable<DocumentInfo>;
                
                if (properties.TryGetValue("AssemblyTimestamp", out var assemblyTimestamp)) {
                    var timestampFilename = assemblyTimestamp + ".timestamp";
                    var timestampFilePath = Path.Combine(solutionCacheDir, timestampFilename);
                    var staticCacheDocument = DocumentInfo.Create(
                        DocumentId.CreateNewId(projectId),
                        timestampFilename,
                        filePath: timestampFilePath);
                    
                    modifiedDocuments = modifiedDocuments.Concat(new []{staticCacheDocument});
                    
                    return modifiedDocuments;
                }
            }
            catch (Exception e) {
                Console.WriteLine("Loading cache files failed: " + e);
            }

            return additionalDocuments;
        }

        private static IEnumerable<DocumentInfo> GetDocuments(IEnumerable<string> sources, string projectDir, ProjectId projectId)
        {
            return sources.Select(filename => GetDocumentInfo(filename, projectDir, projectId))
                          .Where(docInfo => docInfo != null);
        }

        private static DocumentInfo GetDocumentInfo(string filename, string projectDir, ProjectId projectId)
        {
            var filepath = Path.Combine(projectDir, filename);

            if (!File.Exists(filepath))
                return null;

            return DocumentInfo.Create(
                        DocumentId.CreateNewId(projectId),
                        filename,
                        loader: TextLoader.From(
                            TextAndVersion.Create(
                                SourceText.From(File.ReadAllText(filepath), Encoding.Default), VersionStamp.Create())),
                        filePath: filepath);
        }

        private static ParseOptions CreateParseOptions(string defineConstants)
        {
            var parseOptions = new CSharpParseOptions();

            if (!string.IsNullOrWhiteSpace(defineConstants))
                parseOptions = parseOptions.WithPreprocessorSymbols(defineConstants.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
            
            // if (!string.IsNullOrWhiteSpace(langVersion)
            //     && LanguageVersionFacts.TryParse(langVersion, out LanguageVersion languageVersion))
            parseOptions = parseOptions.WithLanguageVersion(LanguageVersion.Latest);
           
            return parseOptions;
        }


        private static IEnumerable<MetadataReference> GetMetadataReferences(string[] references)
        {
            return references.Where(File.Exists)
                             .Select(x => MetadataReference.CreateFromFile(x));
        }
    }
}
