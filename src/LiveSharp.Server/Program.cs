using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LiveSharp.Server
{
    public class Program
    {
        public static readonly int[] HostPorts = { 50540, 30540, 40540 };
        public static string[] Arguments { get; private set; }
        public static void Main(string[] args)
        {
            try {
                Arguments = args;

                var webHostBuilder = CreateWebHostBuilder(args);
                webHostBuilder.Build().Run();
            } catch (Exception e) {
                Console.WriteLine("EXCEPTION: " + e);
                throw;
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args)
        {
            try {
                if (args.Any(a => a == "dashboard"))
                    return CreateDashboardBuilder(args);

                var logger = new ServerLogger();
                if (args.Contains("debug"))
                    logger.IsDebugLoggingEnabled = true;

                var availableHttpsPort = GetAvailableHttpsPort(logger);
                
                if (availableHttpsPort == null) {
                    logger.LogError("Couldn't find available port for HTTPS");
                    Process.GetCurrentProcess().Kill();
                }
                
                var httpEndpoint = $"http://*:{availableHttpsPort}";
                var httpsEndpoint = $"https://*:{availableHttpsPort - 1}";
                
                var urls = new[] { httpEndpoint, httpsEndpoint };
                //var urls = new[] { httpsEndpoint };
                var assemblyLocation = Path.GetDirectoryName(typeof(HostStartup).Assembly.Location) ?? "";

                var webHostBuilder = WebHost.CreateDefaultBuilder(args)
                    .UseUrls(urls)
                    .UseKestrel(opts =>
                    {
                        opts.ConfigureHttpsDefaults(o =>
                        {
                            var certificateLocation = Path.Combine(assemblyLocation, "localhost.livesharp.net.pfx");
                        
                            o.ServerCertificate = new X509Certificate2(certificateLocation, "dS#A^*903DPo");
                        });
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton(logger);
                    })
                    .UseStaticWebAssets()
                    .UseStartup<HostStartup>();

                if (!Environment.CurrentDirectory.EndsWith(Path.DirectorySeparatorChar + "LiveSharp.Server")) {
                    if (!Directory.Exists(Path.Combine(assemblyLocation, "wwwroot")))
                        assemblyLocation = Path.Combine(assemblyLocation, "publish");

                    webHostBuilder.UseContentRoot(assemblyLocation);
                }
                
                return webHostBuilder;
            }
            catch (Exception e) {
                Console.WriteLine("Couldn't start the server: " + e);
                throw;
            }
        }

        private static int? GetAvailableHttpsPort(ServerLogger logger) 
        {
            int? availablePort = null;
            foreach (var port in HostPorts) {
                try {
                    using var httpListener = new DisposableTcpListener(IPAddress.Loopback, port);
                    using var httpsListener = new DisposableTcpListener(IPAddress.Loopback, port - 1);
                    
                    httpListener.Start();
                    httpsListener.Start();

                    availablePort = port;
                    break;
                } catch (Exception) {
                    logger.LogWarning($"Port {port} is in use");
                }
            }

            if (availablePort != null)
                logger.LogMessage($"Found available port: {availablePort.Value}");
            
            return availablePort;
        }

        private static IWebHostBuilder CreateDashboardBuilder(string[] args)
        {
            var nugetPath = getConsoleArgumentValue("/NuGetPackagePath");
            var solutionPath = getConsoleArgumentValue("/SolutionPath");
            var projectDir = getConsoleArgumentValue("/ProjectDir");
            var projectName = getConsoleArgumentValue("/ProjectName");
            var serverVersion = getConsoleArgumentValue("/ServerVersion");
            var isLiveBlazor = args.Any(a => a == "liveblazor");
            var prefix = isLiveBlazor ? "LiveBlazor" : "LiveSharp";
            
            var dashboardAssemblyDir = isLiveBlazor ? Path.Combine(nugetPath, "LiveBlazorWorkspace") : Path.Combine(nugetPath, "Workspace");
            var dashboardAssemblyPath = Path.Combine(dashboardAssemblyDir, $"{prefix}.Dashboard.dll");
            
            var dashboardAssembly = Assembly.LoadFrom(dashboardAssemblyPath);
            var startupType = dashboardAssembly.GetType($"{prefix}.Dashboard.Startup");
            var workspaceInitializerType = dashboardAssembly.GetType($"{prefix}.Dashboard.WorkspaceInitializer");
            
            var workspaceInitializer = Activator.CreateInstance(workspaceInitializerType, nugetPath, solutionPath, projectDir, projectName);

            return WebHost.CreateDefaultBuilder(args)
                .UseUrls("https://*:0")
                .ConfigureAppConfiguration(config =>
                {        
                    var map = new Dictionary<string, string>
                    {
                        {nameof(ServerLogger.IsDebugLoggingEnabled), args.Contains("debug").ToString()}
                    };
                    config.AddInMemoryCollection(map);
                })
                .UseKestrel(opts =>
                {
                    opts.ConfigureHttpsDefaults(o =>
                    {
                        var assemblyLocation = Path.GetDirectoryName(typeof(Program).Assembly.Location);
                        var certificateLocation = Path.Combine(assemblyLocation, "localhost.livesharp.net.pfx");
                        
                        o.ServerCertificate = new X509Certificate2(certificateLocation, "dS#A^*903DPo");
                    });
                })
                .ConfigureServices(services => services.AddSingleton(workspaceInitializerType, workspaceInitializer))
                .ConfigureLogging(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Warning);
                })
                .UseContentRoot(dashboardAssemblyDir)
                .UseStartup(startupType);
            
            string getConsoleArgumentValue(string name)
            {
                var arg = args.FirstOrDefault(a => a.StartsWith(name + "="));
                if (arg == null) {
                    Console.WriteLine($"Argument `{name}` not found");
                    return null;
                }

                var argSplit = arg.Split('=');
                if (argSplit.Length != 2) {
                    Console.WriteLine($"Argument `{name}` has invalid format. Expected '{name}=\"[value]\"'");
                    return null;
                }

                return argSplit[1];
            }
        }
        
    }
}
