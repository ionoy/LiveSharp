using LiveSharp;
using LiveSharp.RuntimeTests;
using LiveSharp.RuntimeTests.Infrastructure;

// Use this attribute to designate which types and methods will be available for runtime code update
[assembly: LiveSharpInject("*")]
[assembly: LiveSharpExclude(typeof(TestLogger))]
[assembly: LiveSharpExclude(typeof(Log))]
[assembly: LiveSharpExclude(typeof(TestsBase))]
// [assembly: LiveSharpExclude(typeof(MyControl))]
// [assembly: LiveSharpExclude("LiveSharp.RuntimeTests.TestRunner*")]
[assembly: LiveSharpExclude("LiveSharp.RuntimeTests.Infrastructure.ReflectionExtensions*")]
[assembly: LiveSharpExclude("LiveSharp.RuntimeTests.ObjectExtensions*")]
[assembly: LiveSharpExclude("CSharpForMarkup*")]

// Uncomment the following line if you want to provide a custom entry point for LiveSharp runtime
// [assembly: LiveSharpStart(typeof(Program), nameof(Program.Main), typeof(string[]))]

// ReSharper disable once CheckNamespace
namespace LiveSharp 
{
    class LiveSharpDashboard : ILiveSharpDashboard
    {
        // This method will be run during the start of your application and every time you update it
        public void Configure(ILiveSharpRuntime app) 
        {
            app.Config.SetValue("pageHotReloadMethod", "build");
            app.UseDefaultXamarinFormsHandler();
        }
        
        public void Run(ILiveSharpRuntime app)
        {
            // Use this method to execute any code in runtime
            // Every time you update this method LiveSharp will invoke it
        }
    } 
}