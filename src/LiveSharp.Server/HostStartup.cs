using LiveSharp.Server.Services;
using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace LiveSharp.Server
{
    public class HostStartup
    {
        private bool _hideDashboard;
        public IConfiguration Configuration { get; }

        public HostStartup(IConfiguration configuration)
        {
            Configuration = configuration;
            _hideDashboard = false;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(opt =>
            {
                opt.AddPolicy("livesharp", builder =>
                {
                    builder.AllowAnyHeader();
                    builder.AllowAnyMethod();
                    builder.AllowAnyOrigin();
                });
            });
            
            var liveSharpSettings = LiveSharpSettings.Load();
            
            services.AddRazorPages();
            services.AddMvc(options =>
            {
                options.EnableEndpointRouting = false;
            }).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddSignalR(opts =>
            {
                opts.MaximumReceiveMessageSize = null;
            });
            services.AddServerSideBlazor();
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            services.AddSingleton<MatchmakingService>();
            services.AddSingleton<LoggingService>();
            services.AddSingleton(liveSharpSettings);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            // Console.WriteLine("LOCATION!!!!   " + typeof(HostStartup).Assembly.Location);
            // Console.WriteLine("LOCATION!!!!   " + env.ContentRootPath);
            // Console.WriteLine("LOCATION!!!!   " + env.WebRootPath);
            app.UseDeveloperExceptionPage();            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }
            
            app.UseStaticFiles();
            //app.UseHttpsRedirection();
            app.UseCors(builder => builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action}/{id?}");
                
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                // endpoints.MapRazorPages();
            });
            
            var addressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();

            lifetime.ApplicationStarted.Register(() =>
            {
                var addresses = addressesFeature.Addresses;
                var fallbackUrl = "https://localhost.livesharp.net:50540";
                var url = addresses.FirstOrDefault(a => a.StartsWith("https://")) ?? fallbackUrl;
                
                url = url.Replace("//localhost:", "//localhost.livesharp.net:")
                         .Replace("//[::]:", "//localhost.livesharp.net:")
                         .Replace("//127.0.0.1:", "//localhost.livesharp.net:");
                
                Console.WriteLine("Welcome to LiveSharp!");
                Console.WriteLine("You can open LiveSharp Dashboard in the browser at " + url);
                var serverLogger = app.ApplicationServices.GetService<ServerLogger>();
                
                if (!_hideDashboard) 
                    OpenBrowser(url);
            });
        }
        
        private bool OpenBrowser(string url)
        {
                
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
                return true;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
                return true;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
                return true;
            }
            return false;
        }
    }
}