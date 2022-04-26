using LiveSharp.Debugging;
using LiveSharp.Runtime.Network;
using LiveSharp.Shared.Api;
using System.Threading.Tasks;

namespace LiveSharp.Support.TestHarness
{
    public class TestHarnessUpdateHandler : ILiveSharpUpdateHandler
    {
        private ILiveSharpRuntime _runtime;
        private ILiveSharpLogger _logger;

        public void Attach(ILiveSharpRuntime runtime)
        {
            _runtime = runtime;
            _logger = runtime.Logger;

            var loggerWrapper = new LoggerWrapper(runtime.Logger);
            var workspace = new LiveSharpWorkspace(loggerWrapper, BroadcastSender);
            var projectInfo = runtime.ProjectInfo;

            workspace.SetLicenseStatus(true);
            
            var sharedProjectInfo = new ProjectInfo {
                NuGetPackagePath = projectInfo.NuGetPackagePath,
                ProjectDir = projectInfo.ProjectDir,
                ProjectName = projectInfo.ProjectName,
                SolutionPath = projectInfo.SolutionPath
            };
            
            workspace.LoadSolution(sharedProjectInfo).ContinueWith(SolutionLoaded);

            TestHarnessTransport.Instance.DebugEventProcessor = new DebugEventProcessor();

            _logger.LogMessage("Test harness update handler started");
        }

        private void SolutionLoaded(Task loadResult)
        {
            if (loadResult.Exception != null) {
                _logger.LogError("Test solution load failed", loadResult.Exception);
            }
        }

        private void BroadcastSender(byte[] buffer, byte contentType, int group)
        {
            TestHarnessTransport.Instance.FeedToRuntime(buffer, contentType, group);
        }

        private void BroadcastSender(ServerMessage serverMessage)
        {
            TestHarnessTransport.Instance.FeedToRuntime(serverMessage);
        }

        public void Dispose()
        {
        }
    }
}