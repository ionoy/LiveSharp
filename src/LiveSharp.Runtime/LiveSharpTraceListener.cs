using System.Diagnostics;

namespace LiveSharp.Runtime
{
    public class LiveSharpTraceListener : TraceListener
    {
        private readonly RuntimeLogger _logger;
        private bool _noStackOverflow;

        public LiveSharpTraceListener(RuntimeLogger logger)
        {
            _logger = logger;
        }

        public override void Write(string message)
        {
            WriteLine(message);
        }

        public override void WriteLine(string message)
        {
            if (_noStackOverflow || _logger.IsRuntimeLoggerWriteLine)
                return;
            _noStackOverflow = true;
            _logger.BroadcastRuntimeLog("trace: " + message);
            _noStackOverflow = false;
        }
    }
}