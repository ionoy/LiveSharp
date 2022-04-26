using System.Diagnostics;
using Microsoft.Build.Framework;

namespace LiveSharp.ServerTask
{
    public class LiveSharpServerTask : ITask
    {
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        public bool Execute()
        {
            var runningServers = Process.GetProcessesByName("WebApplication.exe");
            
            Process.Start(@"WebApplication.exe");
            return true;
        }
    }
}