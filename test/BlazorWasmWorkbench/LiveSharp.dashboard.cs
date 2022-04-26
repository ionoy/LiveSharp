using BlazorWasmWorkbench;
using BlazorWasmWorkbench.Pages;
using LiveSharp;
using Microsoft.AspNetCore.Components;

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
            app.Config.SetValue("disableBlazorCSS", "false");
            
            app.UpdateDiagnosticPanel("New panel", @"<div>privet</div>");
            app.UpdateDiagnosticPanel("New panel 2", @"<div>privet2</div>");
            app.UseDefaultBlazorHandler();

// MyApp myApp = null;
//
// app.OnMethodCallIntercepted(typeof(MyApp), (methodIdentifier, instance, args) => {
//     // Instance will be `null` for static methods
//     if (instance != null) 
//         myApp = (MyApp)instance;
// });
//
// app.OnCodeUpdateReceived(methods => {
//     if (myApp != null) {
//         myApp.Dispose();
//         myApp.Initialize();
//     }
// });
//
// app.OnResourceUpdateReceived((path, content) => {
//     if (path == "app.settings") {
//         myApp.LoadSettings(content);
//     }
// });
        }
        
        public void Run(ILiveSharpRuntime app)
        {
            // Use this method to execute any code in runtime
            // Every time you update this method LiveSharp will invoke it
        }
    }

    class MyApp
    {
        public void Dispose()
        {
            throw new System.NotImplementedException();
        }

        public void Initialize()
        {
            throw new System.NotImplementedException();
        }

        public void LoadSettings(string content)
        {
            throw new System.NotImplementedException();
        }
    }
}