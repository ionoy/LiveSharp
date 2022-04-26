using System.Diagnostics;
using LiveSharp;

// Use this attribute to designate which types and methods will be available for runtime code update
[assembly: LiveSharpInject("*")]

// Uncomment the following line if you want to provide a custom entry point for LiveSharp runtime
[assembly: LiveSharpStart(typeof(BlazorWorkbench.Startup), ".ctor", typeof(Microsoft.Extensions.Configuration.IConfiguration))]
[assembly: LiveSharpSkipStart]

// ReSharper disable once CheckNamespace
namespace LiveSharp 
{
    class LiveSharpDashboard : ILiveSharpDashboard
    {
        // This method will be run during the start of your application and every time you update it
        public void Configure(ILiveSharpRuntime app) 
        {
            app.Config.SetValue("disableBlazorCSS", "false");
            app.UseDefaultBlazorHandler();
            
            // app.OnServerConnected(() =>
            // {
            //     app.UpdateDiagnosticPanel("Blazor", "22");
            // });
        }
        
        public void Run(ILiveSharpRuntime app)
        {
            // Use this method to execute any code in runtime
            // Every time you update this method LiveSharp will invoke it
        }
    } 
}