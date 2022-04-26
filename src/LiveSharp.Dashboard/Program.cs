using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace LiveSharp.Dashboard
{
    public class Program
    {
        public static bool IsDebugProcess { get; private set; }
        public static void Main(string[] args)
        {
            IsDebugProcess = false;

            var projectDir = new DirectoryInfo(Path.GetDirectoryName(typeof(Program).Assembly.Location)).Parent.Parent.Parent;
            var solutionDir = projectDir.Parent.Parent.FullName;
            var nugetPath = Path.Combine(solutionDir, "build");
            var projectName = "LiveSharp.Dashboard";
            var solutionPath = Path.Combine(solutionDir, "LiveSharp.sln");
            var workspaceInitializer = new WorkspaceInitializer(
                nugetPath, 
                solutionPath, 
                projectDir.FullName, 
                projectName);
            // var workspaceInitializer = new WorkspaceInitializer(
            //     "/Users/ionoy/.nuget/packages/livesharp/1.5.16/build/", 
            //     "/Users/ionoy/RiderProjects/WebApplication/WebApplication.sln", 
            //     "/Users/ionoy/RiderProjects/WebApplication/WebApplication/", 
            //     "WebApplication");
            
            CreateHostBuilder(args).ConfigureServices(services =>
            {
                services.AddSingleton(workspaceInitializer);
            }).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://127.0.0.1:0");
                    webBuilder.UseStartup<Startup>();
                });
    }
}