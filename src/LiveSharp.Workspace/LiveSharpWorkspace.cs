using LiveSharp.CSharp;
using LiveSharp.Ide.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using LiveSharp.Infrastructure;
using LiveSharp.LiveXAML;
using LiveSharp.Shared.Network;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using LiveSharpHandler = LiveSharp.CSharp.LiveSharpHandler;

namespace LiveSharp
{
    public class LiveSharpWorkspace
    {
        public Workspace Workspace { get; private set; }
        public bool IsDryRunEnabled { get; set; } = true;
        public LiveSharpHandler SharpHandler { get; }


        private readonly ILogger _logger;
        private readonly Action<byte[], byte, int> _broadcastSender;
        private readonly JobQueue _jobQueue;
        private readonly LiveXamlHandler _liveXamlHandler;
        private readonly CancellationTokenSource _dryRunCancellation = new CancellationTokenSource();
        private readonly ConcurrentDictionary<string, string> _additionalSettings = new ConcurrentDictionary<string, string>();

        private LiveSharpFileWatcher _fileWatcher;
        private RazorHandler _razorHandler;
        private bool _isLicenseValid;
        private Action<TextDocument> _onRazorFileChanged;

        private List<string> _ephemeralDocuments = new();

        public LiveSharpWorkspace(ILogger logger, Action<byte[], byte, int> broadcastSender)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _broadcastSender = broadcastSender ?? throw new ArgumentNullException(nameof(broadcastSender));
            _jobQueue = new JobQueue(logger);

            _liveXamlHandler = new LiveXamlHandler(this, logger);
            SharpHandler = new LiveSharpHandler(this, logger);
        }

        public async Task LoadSolution(LiveSharp.Shared.Api.ProjectInfo projectInfo, bool needWatcherSubscribe = true)
        {
            var sw = new Stopwatch();
            sw.Start();

            try {
                var workspace = await WorkspaceLoader.GetWorkspace(projectInfo.NuGetPackagePath, projectInfo.ProjectDir, projectInfo.ProjectName, _additionalSettings, _logger);

                SharpHandler.SetMainProjectName(projectInfo.ProjectName);
                _razorHandler = new RazorHandler(workspace.CurrentSolution, _additionalSettings, _logger);

                Workspace = workspace;

                _logger.LogMessage("Workspace loading finished in " + sw.ElapsedMilliseconds + $"ms");

                AfterSolutionLoaded(projectInfo.ProjectName, needWatcherSubscribe, workspace);
            } catch (Exception e) {
                _logger.LogError("Compilation failed: " + Environment.NewLine + e);
            } finally {
                sw.Stop();
            }
        }

        private void AfterSolutionLoaded(string projectName, bool needWatcherSubscribe, Microsoft.CodeAnalysis.Workspace workspace)
        {
            if (needWatcherSubscribe) {
                _fileWatcher = new LiveSharpFileWatcher(this, _logger, projectName);
                _fileWatcher.Subscribe(workspace);
            }

            _liveXamlHandler.ReadInitialPropertiesOfXamlFiles(workspace);

            if (IsDryRunEnabled) {
                StartDryRun(projectName);
            }
        }

        private void StartDryRun(string projectName)
        {
            _jobQueue.AddAsyncJob(async () => {
                foreach (var p in Workspace.CurrentSolution.Projects) {
                    var project = p;
                    var razorDocuments = project.AdditionalDocuments.Where(d => d.Name.EndsWith(".razor"));
                    var newDocuments = new List<DocumentInfo>();

                    // foreach (var razorDocument in razorDocuments) {
                    //     var generatedCode = _razorHandler.GetGeneratedCode(razorDocument);
                    //     
                    //     if (!project.TryFindRazorGeneratedDocument(razorDocument.Name, out var generatedDocumentId, out var generatedFileName)) {
                    //         var filePath = Path.Combine(project.FilePath, generatedFileName);
                    //         var newDocument = DocumentInfo.Create(
                    //             DocumentId.CreateNewId(project.Id),
                    //             generatedFileName,
                    //             loader: TextLoader.From(
                    //                 TextAndVersion.Create(
                    //                     SourceText.From(generatedCode, Encoding.Default), VersionStamp.Create())),
                    //             filePath: filePath);
                    //         newDocuments.Add(newDocument);
                    //         
                    //         _ephemeralDocuments.Add(filePath);
                    //     }
                    // }

                    if (newDocuments.Any()) {
                        var newSolution = Workspace.CurrentSolution.AddDocuments(newDocuments.ToImmutableArray());
                        Workspace.TryApplyChanges(newSolution);
                        var updatedProject = newSolution.Projects.FirstOrDefault(p => p.Name == project.Name);
                        
                        if (updatedProject != null)
                            project = updatedProject;
                        else 
                            _logger.LogError($"Couldn't find the updated project for {project.Name}");
                    }
                    
                    foreach (var projectDocument in project.Documents) {
                        if (_dryRunCancellation.IsCancellationRequested)
                            break;

                        try {
                            var text = await projectDocument.GetTextAsync();
                            await SharpHandler.SourceChanged(project.Solution, text.ToString(), projectDocument.Id, true);
                            break;
                        } catch {
                            //continue
                        }
                    }
                }
            }, "Initial compilation " + projectName);
        }

        public RazorBuildEngine GetRazorBuildEngine(Project project)
        {
            var buildEngine = _razorHandler.BuildEngines.FirstOrDefault(e => e.Project.Name == project.Name && e.Project.FilePath == project.FilePath);
        
            if (buildEngine == null)
                _logger.LogWarning($"Couldn't find razor build engine for project '${project.Name}");
        
            return buildEngine;
        }

        internal void CsFileChanged(TextDocument document)
        {
            if (!_isLicenseValid) {
                _logger.LogWarning("Ignoring change because the license has expired");
                return;
            }

            var objPrefix = "obj" + Path.DirectorySeparatorChar;
            if (document.Name.StartsWith(objPrefix, StringComparison.InvariantCultureIgnoreCase))
                return;

            var projectDir = document.Project.FilePath;
            if (document.Name.StartsWith(projectDir + Path.DirectorySeparatorChar + objPrefix, StringComparison.InvariantCultureIgnoreCase))
                return;

            _dryRunCancellation.Cancel();

            _jobQueue.AddAsyncJob(async () => {
                var currentSolution = Workspace?.CurrentSolution;
                if (currentSolution != null)
                    await SharpHandler.FileChanged(document, currentSolution);
                else
                    _logger.LogWarning("Couldn't handle C# update because the solution was not loaded");
            }, "Handling C# update: " + document.FilePath);
        }

        internal void XamlFileChanged(string absolutePath)
        {
            if (!_isLicenseValid) {
                _logger.LogWarning("Ignoring change because the license has expired");
                return;
            }

            // todo find full path from solution
            var documentIds = Workspace.CurrentSolution.GetDocumentIdsWithFilePath(absolutePath);

            if (documentIds.Length > 0) {
                var document = Workspace.CurrentSolution.GetAdditionalDocument(documentIds[0]);
                _jobQueue.AddAsyncJob(() => _liveXamlHandler.FileChangedAsync(absolutePath, document.Name), "Handling XAML update: " + document.Name);
            } else {
                _logger.LogWarning("Can't find document for " + absolutePath);
            }
            //
        }

        public void OnRazorFileChanged(Action<TextDocument> action)
        {
            _onRazorFileChanged = action;
        }

        public void RazorFileChanged(TextDocument document)
        {
            if (!_isLicenseValid) {
                _logger.LogWarning("Ignoring change because the license has expired");
                return;
            }

            var absolutePath = document.FilePath;

            _onRazorFileChanged?.Invoke(document);
            
            if (_razorHandler == null)
                return;

            var documentIds = Workspace.CurrentSolution.GetDocumentIdsWithFilePath(absolutePath);

            if (documentIds.Length > 0) {
                var razorDocument = Workspace.CurrentSolution.GetAdditionalDocument(documentIds[0]);

                if (razorDocument == null)
                    return;
                
                _jobQueue.AddAsyncJob(async () => {
                    var project = razorDocument.Project;
                    var semanticModel = await project.Documents.FirstOrDefault().GetSemanticModelAsync();
                    
                    SharpHandler.PrepareUpdatesAsync(project, semanticModel, document.Name, false);
                    
                    var generatedCode = _razorHandler.GetGeneratedCode(razorDocument);
                    
                     if (project.TryFindRazorGeneratedDocument(razorDocument.Name, out var generatedDocumentId, out _)) {
                         var solution = project.Solution;
                         
                         await SharpHandler.SourceChanged(solution, generatedCode, generatedDocumentId);
                    
                         try {
                             var generatedCsharpDocument = project.GetDocument(generatedDocumentId);
                             var generatedDocumentFilePath = generatedCsharpDocument.FilePath;
                             
                             if (!_ephemeralDocuments.Contains(generatedDocumentFilePath))
                                await File.WriteAllTextAsync(generatedDocumentFilePath, generatedCode);
                             
                             return;
                         } catch (Exception e) {
                             _logger.LogError("Unable to update generated C# code", e);
                         }
                     }
                    
                     _logger.LogError("Couldn't find C# code file for " + razorDocument.Name + ". Please try rebuilding the project.");
                }, "Handling Razor update: " + razorDocument.Name);
            } else {
                _logger.LogWarning("Can't find document for " + absolutePath);
            }
        }

        public void ResourceFileChanged(TextDocument resourceDocument)
        {
            if (!_isLicenseValid) {
                _logger.LogWarning("Ignoring change because the license has expired");
                return;
            }

            if (resourceDocument?.FilePath?.EndsWith(".razor.css", StringComparison.InvariantCultureIgnoreCase) == true) {
                _jobQueue.AddJob(() => {
                    var process = new Process();
                    var projectFilePath = Path.GetDirectoryName(resourceDocument.Project.FilePath);

                    process.StartInfo = new ProcessStartInfo {
                        FileName = "dotnet",
                        Arguments = "msbuild /t:BundleScopedCssFiles -v:diag",
                        WorkingDirectory = projectFilePath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };

                    string scopedCssOutputFullPath = null;
                    process.Start();

                    var nextLineIsBundlePath = false;

                    while (!process.StandardOutput.EndOfStream) {
                        var line = process.StandardOutput.ReadLine();
                        
                        if (line == null)
                            continue;

                        // if (nextLineIsBundlePath) {
                        //     nextLineIsBundlePath = false;
                        //     scopedCssOutputFullPath = line?.Trim();
                        //     _logger.LogMessage(scopedCssOutputFullPath + " updated");
                        // }
                        
                        if (line.Contains("_ScopedCssOutputFullPath")) {
                            var split = line.Split("=");
                            if (split.Length > 1) {
                                scopedCssOutputFullPath = split[1].Trim();
                                _logger.LogMessage(scopedCssOutputFullPath + " updated");
                            }
                        }
                    }

                    process.WaitForExit();

                    if (scopedCssOutputFullPath != null) {
                        var doc = new XDocument(
                            new XElement("Resource",
                                new XAttribute("path", Path.GetFileName(scopedCssOutputFullPath)),
                                new XElement("Content", File.ReadAllText(scopedCssOutputFullPath))));

                        SendBroadcast(doc.ToString(SaveOptions.DisableFormatting), ContentTypes.LiveSharp.ResourceUpdated, BroadcastGroups.LiveSharp);
                    }
                }, "Bundle scoped css");
                return;
            }

            _jobQueue.AddAsyncJob(async () => {
                var xdoc = new XDocument(
                    new XElement("Resource",
                        new XAttribute("path", resourceDocument.Name),
                        new XElement("Content", await resourceDocument.GetTextAsync())));
                SendBroadcast(xdoc.ToString(SaveOptions.DisableFormatting), ContentTypes.LiveSharp.ResourceUpdated, BroadcastGroups.LiveSharp);
            }, "Handling resource update: " + resourceDocument.Name);
        }

        public void FileChanged(TextDocument modifiedDocument)
        {
            var projectDir = Path.GetDirectoryName(modifiedDocument.Project.FilePath);
            var modifiedFileExtension = Path.GetExtension(modifiedDocument.FilePath);
            var projectDirectoryFiles = Directory.GetFiles(projectDir);
            var handler = projectDirectoryFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == $"livesharphandler{modifiedFileExtension}");

            if (handler != null) {
                _logger.LogMessage("Executing custom handler: " + handler + " " + modifiedDocument.FilePath);

                Process.Start(new ProcessStartInfo {
                    FileName = handler,
                    Arguments = modifiedDocument.FilePath ?? "",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = false,
                    UseShellExecute = false
                });
            }
        }

        public void SendBroadcast(string content, byte contentType, int group)
        {
            var buffer = Encoding.Unicode.GetBytes(content);
            SendBroadcast(buffer, contentType, group);
        }

        public void SendBroadcast(byte[] content, byte contentType, int group)
        {
            _broadcastSender(content, contentType, group);
        }

        public void SetLicenseStatus(bool isValid)
        {
            _isLicenseValid = isValid;
        }

        public void Dispose()
        {
            _jobQueue?.Dispose();
            _fileWatcher?.Dispose();
            _dryRunCancellation?.Dispose();
            Workspace?.Dispose();
        }

        // This is used for testing purposes
//        public async Task EnableGlobalDebugging()
//        {
//            foreach (var project in Workspace.CurrentSolution.Projects) {
//                if (project.Name == "LiveSharp.RuntimeTests")
//                    continue;
//                
//                var containsLiveSharpReference = project.MetadataReferences.Any(mr => mr.Display?.IndexOf("LiveSharp.Runtime.dll", StringComparison.InvariantCultureIgnoreCase) != -1);
//                if (!containsLiveSharpReference && project.Name != "LiveSharp.Runtime")
//                    continue;
//                
//                foreach (var document in project.Documents) {
//                    await _liveSharpHandler.PrepareUpdatesAsync(document, true, true);
//                }
//            }
//        }

    }
}