using LiveSharp.Server.Services;
using LiveSharp.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LiveSharp.Server.Controllers
{
    public class ConnectController : Controller
    {
        private readonly MatchmakingService _matchmakingService;
        private readonly LoggingService _loggingService;

        public ConnectController(MatchmakingService matchmakingService, LoggingService loggingService)
        {
            _matchmakingService = matchmakingService;
            _loggingService = loggingService;
        }
        
        [HttpPost]
        public async Task<IActionResult> Runtime(string projectInfo)
        {
            try {
                var projectInfoObject = ProjectInfo.Deserialize(projectInfo);
                var serverAddress = await _matchmakingService.ProjectInfoReceived(projectInfoObject);
            
                return Ok(new ServerAddress {
                    Url = serverAddress.serverAddress,
                    TcpPort = serverAddress.tcpServerPort
                }.Serialize());
            } catch (DebugServerAlreadyLoadingException) {
                return Problem();
            }
        }

        [HttpPost]
        public IActionResult Workspace(string projectId, string serverAddress, int tcpServerPort)
        {
            _matchmakingService.WorkspaceAddressReceived(projectId, serverAddress, tcpServerPort);
            
            return Ok();
        }

        [HttpPost]
        public IActionResult WorkspaceLog(string projectName, string logText)
        {
            _loggingService.AppendWorkspaceLog(projectName, logText);
            return Ok();
        }
        
        [HttpPost]
        public IActionResult RuntimeLog(string projectName, string logText)
        {
            _loggingService.AppendRuntimeLog(projectName, logText);
            return Ok();
        }
    }
}