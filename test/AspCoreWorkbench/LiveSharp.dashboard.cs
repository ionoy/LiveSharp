using System.Linq;
using System.Text;
using AspCoreWorkbench.Controllers;
using LiveSharp;
using Microsoft.AspNetCore.Mvc;

// Use this attribute to designate which types and methods will be available for runtime code update
[assembly: LiveSharpInject("*")]

// Uncomment the following line if you want to provide a custom entry point for LiveSharp runtime
//[assembly: LiveSharpStart(typeof(Program), nameof(Program.Main), typeof(string[]))]

// ReSharper disable once CheckNamespace
namespace LiveSharp 
{
    class LiveSharpDashboard : ILiveSharpDashboard
    {
        // This method will be run during the start of your application and every time you update it
        public void Configure(ILiveSharpRuntime app) 
        {
app.OnMethodCallIntercepted(typeof(WeatherForecastController), (identifier, instance, args) =>
{
    var controller = (Controller)instance;
    var sb = new StringBuilder();

    sb.Append($"<strong>{controller.Request.Path}</strong><br/>");
    sb.Append("<table>");

    foreach (var header in controller.Request.Headers)
        sb.Append($"<tr><td>{header.Key}</td><td>{header.Value}</td></tr>");

    sb.Append("</table>");

    app.UpdateDiagnosticPanel("Headers", sb.ToString());
});
        }
        
        public void Run(ILiveSharpRuntime app)
        {
            // Use this method to execute any code in runtime
            // Every time you update this method LiveSharp will invoke it
        }
    } 
}