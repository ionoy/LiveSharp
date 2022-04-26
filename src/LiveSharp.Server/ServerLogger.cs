using System;
using System.IO;
using System.Threading.Tasks;

namespace LiveSharp.Server
{
    public class ServerLogger
    {
        public event Action<string> NewLog;
        
        private readonly Action<string> _onWriteLine;
        private readonly string LogFilePath;
        private Task _appendTask = Task.Run(() => { });

        public ServerLogger(Action<string> onWriteLine = null)
        {
            _onWriteLine = onWriteLine;
            var tempPath = Path.GetTempPath();
            var random = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
            var directory = Path.Combine(tempPath, "LiveSharp");

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            LogFilePath = Path.Combine(directory, "server-" + random + ".log");
        }

        public bool IsDebugLoggingEnabled { get; set; }

        public void LogError(string error)
        {
            WriteLine("Error: " + error);
        }

        public void LogError(string errorMessage, Exception e)
        {
            LogError(errorMessage + Environment.NewLine + e);
        }

        public void LogMessage(string message)
        {
            WriteLine(message);
        }

        public void LogWarning(string warning)
        {
            WriteLine("Warning: " + warning);
        }

        public void LogDebug(string debugInfo)
        {
            if (IsDebugLoggingEnabled)
                WriteLine("debug: " + debugInfo);
        }

        public void WriteLine(string text)
        {
            var time = DateTime.Now.ToString("HH:mm:ss");

            Console.WriteLine("LiveSharp: " + text);

            FileWrite(time + " " + text + Environment.NewLine);
            
            NewLog?.Invoke(text);
        }

        private void FileWrite(string text)
        {
            _appendTask = _appendTask.ContinueWith(_ =>
            {
                File.AppendAllText(LogFilePath, text);
            });
        }
    }
}