using System.Reflection;
using LiveSharp;
using LiveSharp.Runtime.Network;
using LiveSharp.Runtime.Parsing;
using Microsoft.Extensions.Hosting;

namespace LaunchPad.Runtime;

public class LaunchPadRuntime
{
    public static LaunchPadAssemblyLoadContext CurrentAssemblyLoadContext { get; set; } = new ();
    public static bool IsRunning { get; set; }
    public static IHost Host { get; set; }
    
    public static Action<Assembly> UpdateHandler { get; set; }

    public static void StartListeningForNewAssemblies(string[] args)
    {
        Task.Run(async () => {
            //var buildDir = Path.GetDirectoryName(typeof(LiveSharpRuntime).Assembly.Location);
            var buildDir = @"c:\projects\livesharp\build";
            var updateFilename = Path.Combine(buildDir, "livesharp.update");
            var projectName = "LiveSharpLaunchPadSample";
            var messageParser = new MessageParser(null);
            var allAssemblies = new Dictionary<string, LiveSharpAssemblyUpdate>();

            // LiveSharpRuntime.Start(null, @"C:\Projects\.temp\LiveSharpLaunchPadSample\", projectName, @"C:\Projects\.temp\LiveSharpLaunchPadSample\LiveSharpLaunchPadSample\", "",
            //     buildDir);

            messageParser.MessageParsed += (sender, eventArgs) => {
                var assemblyBuffer = eventArgs.Message.Content;
                var assemblyUpdate = Deserialize.Object<LiveSharpAssemblyUpdate>(assemblyBuffer);

                allAssemblies[assemblyUpdate.Name] = assemblyUpdate;

                if (assemblyUpdate.Name == projectName)
                {
                    Console.WriteLine("Unloading assembly load context");
                    CurrentAssemblyLoadContext.Unload();
                    CurrentAssemblyLoadContext = new LaunchPadAssemblyLoadContext(allAssemblies.Values.ToArray());

                    var projectAssembly = CurrentAssemblyLoadContext.LoadFromAssemblyName(new AssemblyName(projectName));

                    UpdateHandler?.Invoke(projectAssembly);
                }
            };

            while (true)
            {
                if (File.Exists(updateFilename))
                {
                    var updateBuffer = File.ReadAllBytes(updateFilename);
                    File.Delete(updateFilename);

                    messageParser.Feed(updateBuffer, updateBuffer.Length);
                }

                await Task.Delay(500);
            }
        });
    }
}