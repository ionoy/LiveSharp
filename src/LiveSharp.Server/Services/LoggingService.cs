using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace LiveSharp.Server.Services
{
    public class LoggingService
    {
        private readonly ServerLogger _serverLogger;
        private readonly Subject<LogItem> _workspaceLogs = new();
        private readonly Subject<LogItem> _runtimeLogs = new();
        private readonly Subject<LogItem> _serverLogs = new();

        public IObservable<LogItem> WorkspaceLogs => _workspaceLogs.AsObservable();
        public IObservable<LogItem> RuntimeLogs => _runtimeLogs.AsObservable();
        public IObservable<LogItem> ServerLogs => _serverLogs.AsObservable();

        public LoggingService(ServerLogger serverLogger)
        {
            _serverLogger = serverLogger;
            _serverLogger.NewLog += str => AppendServerLog($"{DateTime.Now:HH:mm:ss.fff}: {str}");
        }

        public void AppendWorkspaceLog(string projectId, string text)
        {
            _workspaceLogs.OnNext(new LogItem(projectId, text, LogSource.Workspace));
        }

        public void AppendRuntimeLog(string projectId, string text)
        {
            _runtimeLogs.OnNext(new LogItem(projectId, text, LogSource.Runtime));
        }

        public void AppendServerLog(string text)
        {
            _serverLogs.OnNext(new LogItem("", text, LogSource.Server));
        }
    }

    public record LogItem(string ProjectId, string Text, LogSource LogSource);
    public enum LogSource { Workspace, Runtime, Server }
}