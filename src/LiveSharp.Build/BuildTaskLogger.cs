using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;

namespace LiveSharp.Build
{
    public class BuildTaskLogger
    {
        private static string _logFilePath;
        private static System.Threading.Tasks.Task _appendTask = System.Threading.Tasks.Task.Run(() => { });
        private ITask _task;

        public BuildTaskLogger(ITask task)
        {
            var tempPath = Path.GetTempPath();
            var random = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
            var directory = Path.Combine(tempPath, "LiveSharp");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            _logFilePath = Path.Combine(directory, "buildtask-" + random + ".log");
            _task = task;
        }

        public void WriteLine(string text)
        {
            FileWrite(text + Environment.NewLine);
        }

        private void FileWrite(string text)
        {
            _appendTask = _appendTask.ContinueWith(_ => {
                var time = DateTime.Now.ToShortTimeString();
                text = time + ": " + text;
                Debug.Write(text);
                Console.Write(text);
                File.AppendAllText(_logFilePath, text);
            });
        }

        public void LogError(string error)
        {
            WriteLine("Error: " + error);
            _task.BuildEngine.LogErrorEvent(new BuildErrorEventArgs("Custom", "", "", 0, 0, 0, 0, error, "", "LiveSharp build task"));
        }

        public void LogMessage(string message)
        {
            WriteLine("Message: " + message);
            _task.BuildEngine.LogMessageEvent(new BuildMessageEventArgs(message, "", "LiveSharp build task", MessageImportance.Normal));
        }

        public void LogWarning(string warning)
        {
            WriteLine("Warning: " + warning);
            _task.BuildEngine.LogWarningEvent(new BuildWarningEventArgs("Custom", "", "", 0, 0, 0, 0, warning, "", "LiveSharp build task"));
        }
    }
}