using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using LiveSharp.Dashboard.Services;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;

namespace LiveSharp.Dashboard
{
    public class Startup
    {
        private bool _hideDashboard;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            services.AddSignalR(opts =>
            {
                opts.MaximumReceiveMessageSize = null;
            });
            services.AddRazorPages();
            services.AddMvc(options =>
            {
                options.EnableEndpointRouting = false;
            }).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            services.AddServerSideBlazor();
            services.AddResponseCompression(opts =>
            {
                opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                    new[] { "application/octet-stream" });
            });

            var logger = new DashboardLogger();

            try {
                var isDebugLoggingEnabled = Configuration[nameof(ILogger.IsDebugLoggingEnabled)];
                if (isDebugLoggingEnabled != null && bool.Parse(isDebugLoggingEnabled))
                    logger.IsDebugLoggingEnabled = true;
                
                _hideDashboard = Configuration["hide-dashboard"] != null;
            }
            catch (Exception e) {
                logger.LogError("Loading IsDebugLoggingEnabled configuration failed", e);
            }
            
            services.AddSingleton<ILogger>(logger);
            // also register as DashboardLogger
            services.AddSingleton(logger);
            services.AddSingleton<DebuggingService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            
            app.UseCors(builder => builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());
            
            //app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapHub<BlazorHub>("/livesharp");
            });
            
            var workspaceInitializer = app.ApplicationServices.GetService<WorkspaceInitializer>();
            
            if (workspaceInitializer == null)
                throw new Exception("WorkspaceInitializer wasn't added to the application services");

            var dashboardLogger = app.ApplicationServices.GetService<DashboardLogger>();
            var debuggingService = app.ApplicationServices.GetService<DebuggingService>();
            var addressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
            var blazorHubContext = app.ApplicationServices.GetService<IHubContext<BlazorHub>>();
            
            app.UseHttpsRedirection();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });
            
            lifetime.ApplicationStarted.Register(() => {
                var serverAddress = addressesFeature.Addresses.FirstOrDefault();

                if (serverAddress == null)
                    throw new Exception("Server address is unknown");
                
                workspaceInitializer.Start(dashboardLogger, serverAddress, debuggingService, blazorHubContext);
            });
        }
    }
}