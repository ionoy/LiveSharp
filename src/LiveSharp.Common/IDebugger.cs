using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiveSharp.Common
{
    public interface IDebugger
    {
        Task StartDebuggingMethod(InjectedMethodInfo methodInfo);
        Task StopDebuggingMethod(InjectedMethodInfo methodInfo);
    }
}