using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LiveSharp.Infrastructure
{
    class JobQueue
    {
        Task _jobQueue = Task.FromResult(true);
        
        private readonly CancellationTokenSource _cts;
        private readonly ILogger _logger;
        private readonly object _lock = new object();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        
        public JobQueue(ILogger logger)
        {
            _cts = new CancellationTokenSource();
            _logger = logger;
        }

        public void AddJob(Action action, string jobName)
        {
            lock (_lock)
            {
                _jobQueue = _jobQueue.ContinueWith(_ =>
                {
                    try
                    {
                        _logger.LogMessage("Starting: " + jobName);
                        _stopwatch.Start();
                        
                        action();
                        
                        _logger.LogMessage("Finished: " + jobName + " (" + _stopwatch.ElapsedMilliseconds + ")");
                        _stopwatch.Reset();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Job failed: " + jobName + Environment.NewLine + e);
                    }
                }, _cts.Token);
            }
        }

        public void AddAsyncJob(Func<Task> action, string jobName)
        {
            lock (_lock)
            {
                _jobQueue = _jobQueue.ContinueWith(async _ =>
                {
                    try
                    {
                        _logger.LogMessage("Starting: " + jobName);
                        _stopwatch.Start();
                        
                        await action();
                        
                        _logger.LogMessage("Finished: " + jobName + " (" + _stopwatch.ElapsedMilliseconds + ")");
                        _stopwatch.Reset();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Job failed: " + jobName + Environment.NewLine + e);
                    }
                }, _cts.Token);
            }
        }
        
        public void Dispose()
        {
            lock (_lock) {
                _cts.Cancel();
            }
        }
    }
}
