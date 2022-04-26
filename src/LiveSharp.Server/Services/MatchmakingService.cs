using LiveSharp.Shared.Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Reflection;
using System.Threading.Tasks;

namespace LiveSharp.Server.Services
{
    public class MatchmakingService
    {
        private readonly ServerLogger _logger;
        private readonly ConcurrentDictionary<string, WorkspaceInfo> _workspaces = new();
        private readonly Subject<WorkspaceAddress> _workspaceInfoReceived = new();
        private readonly Subject<ConcurrentDictionary<string, WorkspaceInfo>> _workspacesUpdated = new();
        private bool _debugServerAlreadyLoading = false;
        private readonly ConcurrentDictionary<string, bool> _waitingForWorkspacesToLoad = new();

        public IObservable<ConcurrentDictionary<string, WorkspaceInfo>> WorkspacesUpdated =>
            _workspacesUpdated.AsObservable();

        public MatchmakingService(ServerLogger logger)
        {
            _logger = logger;

            Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ => RemoveDeadWorkspaces());
        }

        public IReadOnlyList<WorkspaceInfo> GetWorkspaces() => _workspaces.Values.ToArray();
        
        public async Task<WorkspaceAddress> ProjectInfoReceived(ProjectInfo projectInfo)
        {
            var projectId = projectInfo.GetProjectId();
            
            try {
                _logger.LogDebug($"Project info received {projectId}");
                _logger.LogDebug($"Waiting for workspaces `{_workspaces.Count}`");
                
                if (_waitingForWorkspacesToLoad.ContainsKey(projectId)) {
                    _logger.LogDebug("Wait for the original workspace");
                    throw new Exception("Wait for the original workspace");
                }
            
                if (_workspaces.TryGetValue(projectId, out var workspaceInfo)) {
                    if (!workspaceInfo.Process.HasExited) {
                        _workspaces[projectId] = new WorkspaceInfo(workspaceInfo.Process, projectInfo, workspaceInfo.Address);
                        _workspacesUpdated.OnNext(_workspaces);
                    
                        return workspaceInfo.Address;
                    }
                
                    _workspaces.TryRemove(projectId, out _);
                }
            
                _waitingForWorkspacesToLoad[projectId] = true;
                 
                workspaceInfo = await StartAndWaitForWorkspace(projectId, projectInfo);

                _workspaces[projectId] = workspaceInfo;
                _workspacesUpdated.OnNext(_workspaces);
            
                return workspaceInfo.Address;
            } finally {
                _waitingForWorkspacesToLoad.TryRemove(projectId, out _);
            }
        }

        private async Task<WorkspaceInfo> StartAndWaitForWorkspace(string projectId, ProjectInfo projectInfo)
        {
            var currentProcess = Process.GetCurrentProcess();
            var processName = currentProcess.MainModule?.FileName ?? "livesharp";
            var args = "";
            
            if (currentProcess.ProcessName == "dotnet")
                args += "run --no-build -- ";
            
            args += string.Join(" ", Program.Arguments);
            var version = typeof(MatchmakingService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            
            var solutionPath = projectInfo.SolutionPath;
            var projectDir = projectInfo.ProjectDir;
            
            if (string.IsNullOrWhiteSpace(solutionPath) || solutionPath == "*Undefined*")
                solutionPath = projectDir;

            args += $" /SolutionPath=\"{solutionPath}\"";
            args += $" /ProjectName=\"{projectInfo.ProjectName}\"";
            args += $" /ProjectDir=\"{projectDir}\"";
            args += $" /NuGetPackagePath=\"{projectInfo.NuGetPackagePath}\"";
            args += $" /ServerVersion=\"{version}\"";
            args += $" dashboard";
            
            if (projectInfo.IsLiveBlazor)
                args += " liveblazor";

            if (_logger.IsDebugLoggingEnabled)
                args += " debug";

            _logger.LogMessage("Starting dashboard process with arguments: " + args);
            
            var process = Process.Start(processName, $"{args}");
            var result = await _workspaceInfoReceived
                .Where(a => a.projectId == projectId)
                .Timeout(TimeSpan.FromSeconds(20))
                .Take(1)
                .ToTask();
            
            return new WorkspaceInfo(process, projectInfo, result);
        }

        public void WorkspaceAddressReceived(string projectId, string serverAddress, int tcpServerPort)
        {
            _workspaceInfoReceived.OnNext(new WorkspaceAddress(projectId, serverAddress, tcpServerPort));
        }
        public void StopWorkspace(WorkspaceInfo workspace)
        {
            if (workspace.Process != null && !workspace.Process.HasExited) {
                workspace.Process.Kill();
                RemoveWorkspace(workspace.ProjectInfo.GetProjectId());
            }
        }
        
        private void RemoveDeadWorkspaces()
        {
            foreach (var workspace in _workspaces.Values) {
                if (workspace.Process.HasExited) {
                    RemoveWorkspace(workspace.ProjectInfo.GetProjectId());
                }
            }
        }
        
        private void RemoveWorkspace(string projectId)
        {
            _workspaces.TryRemove(projectId, out _);
            _workspacesUpdated.OnNext(_workspaces);
        }
    }

    public record WorkspaceInfo(Process Process, ProjectInfo ProjectInfo, WorkspaceAddress Address);

    public record WorkspaceAddress(string projectId, string serverAddress, int tcpServerPort);
}