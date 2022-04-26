using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace LiveSharp.Build
{
    public class PlatformCheckTask : ITask
    {
        [Required]
        public string ProjectCapability { get; set; }
        [Required]
        public string PackageReferences { get; set; }
        [Required]
        public string Content { get; set; }
        
        public string ProjectDir { get; set; }

        [Output]
        public string Platform { get; set; }
        
        public string MSBuildThisFileDirectory { get; set; }

        public bool Execute()
        {
            var platforms = new List<string>();
            Platform = ";";
            
            if (ProjectCapability.IndexOf("XamarinForms", StringComparison.Ordinal) != -1 || 
                PackageReferences.IndexOf("Xamarin.Forms", StringComparison.InvariantCultureIgnoreCase) != -1) {
                platforms.Add("Xamarin.Forms");
            } 
            
            if (ProjectCapability.IndexOf("AspNetCore", StringComparison.Ordinal) != -1 || ProjectCapability.IndexOf("DotNetCoreRazor", StringComparison.Ordinal) != -1) {
                var mostLikelyBlazor = Content.IndexOf("_Imports.razor", StringComparison.Ordinal) != -1 ||
                                       Content.IndexOf("App.razor", StringComparison.Ordinal) != -1 ||
                                       PackageReferences.IndexOf("Microsoft.AspNetCore.Components", StringComparison.Ordinal) != -1;
                
                if (PackageReferences.IndexOf("Microsoft.AspNetCore.Components.WebAssembly", StringComparison.Ordinal) != -1 && !PackageReferences.Contains("Microsoft.AspNetCore.Components.WebAssembly.Server"))
                    platforms.Add("BlazorWASM");                
                else if (mostLikelyBlazor)
                    platforms.Add("BlazorServer");
            }
            
            if (PackageReferences.IndexOf("Uno.UI.WebAssembly", StringComparison.OrdinalIgnoreCase) != -1) {
                platforms.Add("UnoWASM");
            } else if (PackageReferences.IndexOf("Uno.UI", StringComparison.OrdinalIgnoreCase) != -1) {
                platforms.Add("UnoServer");
            }

            Platform = string.Join(";", platforms);

            if (!string.IsNullOrWhiteSpace(ProjectDir)) {
                var configFilePath = Path.Combine(ProjectDir, "LiveSharp.dashboard.cs");

                if (!File.Exists(configFilePath)) {
                    CreateConfigFile(configFilePath, Platform);
                }

                var rulesFilename = Path.Combine(ProjectDir, "livesharp.rules");
                if (File.Exists(rulesFilename))
                    File.Delete(rulesFilename);
                    
                var configFilename = Path.Combine(ProjectDir, "livesharp.config");
                if (File.Exists(configFilename))
                    File.Delete(configFilename);
            }
            
            return true;
        }

        private void CreateConfigFile(string configFilePath, string platform)
        {
            var configSource = GetConfigFileTemplate();
            var newLine = Environment.NewLine;
            var initializationCode = "";
            
            if (platform.Contains("Xamarin.Forms"))
                initializationCode +=
                    $"app.Config.SetValue(\"pageHotReloadMethod\", \"build\");{newLine}            app.UseDefaultXamarinFormsHandler();{newLine}";
            
            if (platform.Contains("BlazorServer"))
                initializationCode += $"app.Config.SetValue(\"disableBlazorCSS\", \"false\");{newLine}            app.UseDefaultBlazorHandler();{newLine}";
            
            if (platform.Contains("BlazorWASM"))
                initializationCode += $"app.Config.SetValue(\"disableBlazorCSS\", \"false\");{newLine}            app.UseDefaultBlazorHandler();{newLine}";
            
            if (platform.Contains("UnoServer"))
                initializationCode += $"app.Config.SetValue(\"pageHotReloadMethod\", \"build\");{newLine}            app.UseDefaultUnoHandler();{newLine}";
            
            if (platform.Contains("UnoWASM"))
                initializationCode += $"app.Config.SetValue(\"pageHotReloadMethod\", \"build\");{newLine}            app.UseDefaultUnoHandler();{newLine}";

            configSource = string.Format(configSource, initializationCode);
            
            File.WriteAllText(configFilePath, configSource);
        }

        private string GetConfigFileTemplate()
        {
            return File.ReadAllText(Path.Combine(MSBuildThisFileDirectory, "_config.cs.template"));
        }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }
    }
}
