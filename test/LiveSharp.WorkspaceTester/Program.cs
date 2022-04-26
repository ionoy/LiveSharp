using System;
using System.IO;
using LiveSharp.Common.Events;

namespace LiveSharp.WorkspaceTester
{
    class Program
    {
        static void Main(string[] args)
        {
            var ws = new LiveSharpWorkspace(new TestLogger(), new EventBus(), ((b, c, a) => {})) {
                IsDryRunEnabled = false
            };
            
            var cd = Environment.CurrentDirectory;
            
            ws.LoadSolution(Path.GetFullPath(cd + @"\..\..\..\..\..\build"), 
                Path.GetFullPath(cd + @"\..\..\..\..\..\LiveSharp.sln"), 
                Path.GetFullPath(cd + @"\..\..\..\..\..\test\LiveSharp.RuntimeTests"), "LiveSharp.RuntimeTests", false).Wait();
        }
    }
}