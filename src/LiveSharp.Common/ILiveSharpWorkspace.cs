using System;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.Common
{
    public interface ILiveSharpWorkspace : IDisposable
    {
        IInjectedMethodsService InjectedMethodsService { get; }
        IDebugger Debugger { get; }
        Task LoadSolution(string nugetPath, string solutionPath, string projectDir, string projectName, bool needWatcherSubscribe = true);
    }
}