#define USE_LOCAL_SERVER

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSharp.ServerTest
{
    class ServerProcess : IDisposable
    {
        private Process _process;
        private TaskCompletionSource _waitingTask;
        private string _waitingText;

        private static readonly Lazy<ServerProcess> _instance = new(() => new ServerProcess());
        private Task _timeoutTask;
        private CancellationTokenSource _timeoutCts;
        public static ServerProcess Instance => _instance.Value;
        
        private ServerProcess() {}
        
        public void Start()
        {
#if USE_LOCAL_SERVER
            _process = new Process {
                StartInfo = new ProcessStartInfo("dotnet") {
                Arguments = "run",
                WorkingDirectory = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\..\src\LiveSharp.Server"),
                UseShellExecute = false, 
                RedirectStandardOutput = true, 
                CreateNoWindow = true
            }};
#else
            _process = new Process {
                StartInfo = new ProcessStartInfo("livesharp", "debug") {
                    UseShellExecute = false, 
                    RedirectStandardOutput = true, 
                    CreateNoWindow = true
                }
            };
            
#endif

            _process.OutputDataReceived += OutputHandler;
            _process.Start();
            _process.BeginOutputReadLine();
        }

        private void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (_waitingTask != null && !_waitingTask.Task.IsCompleted) {
                if (e.Data?.Contains(_waitingText ?? "") == true) {
                    _waitingTask.SetResult();
                }
            }
            
            Console.WriteLine(e.Data);
        }

        public void DoAndWaitForOutput(Action work, string outputToWait, int timeout = 20000)
        {
            try {
                _waitingTask = new TaskCompletionSource();
                _waitingText = outputToWait;
            
                _timeoutCts?.Cancel();
                _timeoutCts = new CancellationTokenSource();
                _timeoutTask = Task.Delay(timeout, _timeoutCts.Token).ContinueWith(WaitingTimeout);

                work();
            
                _waitingTask.Task.Wait();
            } catch {
                _process.Kill();
                throw;
            }
        }

        private void WaitingTimeout(Task sourceTask)
        {
            if (sourceTask.IsCompletedSuccessfully) {
                _waitingTask.SetException(new Exception($"Waiting for server timed out: {_waitingText}"));
            }
        }

        public void Dispose()
        {
            try {
                if (!_process.HasExited) {
                    _process?.Kill(true);
                    _process?.WaitForExit();                
                }
            } catch {
            }
        }
    }
}