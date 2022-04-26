using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace LiveSharp
{
    class LiveSharpFileWatcher : IDisposable
    {
        private readonly LiveSharpWorkspace _workspace;
        private readonly ILogger _logger;
        private readonly string _mainProjectName;
        private readonly ConcurrentDictionary<string, DateTime> _writeTimes = new ConcurrentDictionary<string, DateTime>();
        private readonly List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private IDisposable _watchersObservable;
        private Task _scanTask;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _isDisposed = false;

        public LiveSharpFileWatcher(LiveSharpWorkspace workspace, ILogger logger, string mainProjectName)
        {
            _workspace = workspace;
            _logger = logger;
            _mainProjectName = mainProjectName;
        }

        public void Subscribe(Workspace workspace)
        {
            var solution = workspace.CurrentSolution;
            var documents = solution.Projects.SelectMany(p => p.Documents.Concat(p.AdditionalDocuments)).ToArray();

            ScanDocuments(documents);
            
            _watchersObservable = solution.Projects
                                            .ToObservable()
                                            .SelectMany(p => CreateWatcher(p.FilePath))
                                            .Throttle(TimeSpan.FromMilliseconds(50))
                                            .Subscribe(_ => ScanDocuments(documents));
        }

        private void QueueScan(TextDocument[] documents)
        {
            _cts = new CancellationTokenSource();
            _scanTask = Task.Delay(TimeSpan.FromMilliseconds(500), _cts.Token)
                            .ContinueWith(_ => ScanDocuments(documents), _cts.Token);
        }
        
        private void ScanDocuments(TextDocument[] documents)
        {
            _cts?.Cancel();
            
            var modifiedDocuments = new List<TextDocument>();
            
            foreach (var document in documents) {
                if (document == null || document.FilePath == null)
                    continue;
                
                var lastWriteTime = File.GetLastWriteTime(document.FilePath);
                var documentId = document.Project.Name + ":" + document.FilePath;
                
                if (_writeTimes.TryGetValue(documentId, out var previousWriteTime) && lastWriteTime > previousWriteTime)
                    modifiedDocuments.Add(document);

                _writeTimes[documentId] = lastWriteTime;
            }

            foreach (var documentGroup in modifiedDocuments.GroupBy(d => d.FilePath)) {
                var documentGroupArray = documentGroup.ToArray();
                var modifiedDocument = documentGroupArray[0];

                if (documentGroupArray.Length > 1) {
                    modifiedDocument = documentGroupArray.FirstOrDefault(d => d.Project.Name == _mainProjectName) ??
                                       modifiedDocument;
                }

                var extension = Path.GetExtension(modifiedDocument.FilePath);
                    
                if (string.Equals(extension, ".cs", StringComparison.InvariantCultureIgnoreCase)) {
                    _workspace.CsFileChanged(modifiedDocument);
                }
                    
                if (string.Equals(extension, ".xaml", StringComparison.InvariantCultureIgnoreCase)) {
                    _workspace.XamlFileChanged(modifiedDocument.FilePath);
                }
                    
                if (string.Equals(extension, ".razor", StringComparison.InvariantCultureIgnoreCase)) {
                    _workspace.RazorFileChanged(modifiedDocument);
                }
                    
                if (string.Equals(extension, ".css", StringComparison.InvariantCultureIgnoreCase)) {
                    _workspace.ResourceFileChanged(modifiedDocument);
                }
                
                _workspace.FileChanged(modifiedDocument);
            }
            
            if (!_isDisposed)
                QueueScan(documents);
        }

        private IObservable<EventPattern<FileSystemEventArgs>> CreateWatcher(string directoryToWatch)
        {
            var watcher = new FileSystemWatcher(directoryToWatch)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            
            watcher.EnableRaisingEvents = true;
            
            _watchers.Add(watcher);

            _logger.LogMessage($"watcher created at {directoryToWatch}");

            return Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => watcher.Changed += h, h => watcher.Changed -= h);
        }

        public void Dispose()
        {
            _isDisposed = true;
            
            _watchersObservable?.Dispose();
            _cts.Cancel();
            
            foreach (var watcher in _watchers) watcher.Dispose();
        }
    }
}
