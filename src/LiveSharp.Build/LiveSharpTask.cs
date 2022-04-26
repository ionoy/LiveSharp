using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using LiveSharp.Rewriters;
using System.Diagnostics;

namespace LiveSharp.Build
{
    public class LiveSharpTask : ITask
    {
        private BuildTaskLogger _log;

        [Required]
        public string AssemblyPath { get; set; }

        [Required]
        public string ProjectDir { get; set; }

        [Required]
        public string IntermediateOutputPath { get; set; }

        [Required]
        public string SolutionPath { get; set; }

        [Required]
        public string ProjectName { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string NuGetPackagePath { get; set; }

        [Required]
        public string References { get; set; }

        [Required]
        public string Sources { get; set; }
        
        public string Content { get; set; }

        public string EmbeddedResource { get; set; }

        public string LangVersion { get; set; }
        
        public string DefineConstants { get; set; }
        
        public ITaskItem[] RazorComponentWithTargetPath { get; set; }
        
        public string RootNamespace { get; set; }
        
        public string ProjectReferences { get; set; }
        
        public string Analyzers { get; set; }
        public string AnalyzerConfigFiles { get; set; }
        
        // [Output]
        public string IgnoredFiles { get; set; }
        public string OutputType { get; set; }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        public bool Execute()
        {
            _log = new BuildTaskLogger(this);

            // Debugger.Launch();
            
            SolutionPath = SolutionPath != "*Undefined*" ? SolutionPath : ProjectDir;
            ProjectReferences = ProjectReferences ?? "";
            
            var builder = new MainAssemblyRewriter(
                AssemblyPath, 
                References,
                new RewriteLogger(_log.LogMessage, _log.LogWarning, _log.LogError, ((error, ex) => _log.LogError(error + Environment.NewLine + ex))), 
                true);
            var result = builder.ProcessEverything(
                ProjectDir,
                SolutionPath,
                ProjectName,
                NuGetPackagePath,
                ProjectReferences,
                out var isNonMsBuildConfiguration);

            if (!builder.AlreadyInjected && !isNonMsBuildConfiguration)
                SaveProjectInfo();

            return result;
        }

        private void SaveProjectInfo()
        {
            var solutionCacheDir = Path.Combine(NuGetPackagePath, "SolutionCache");

            if (!Directory.Exists(solutionCacheDir))
                Directory.CreateDirectory(solutionCacheDir);

            var projectListPath = Path.Combine(solutionCacheDir, "project.list");
            var projectLines = File.Exists(projectListPath) 
                                ? FileReadAllLines(projectListPath).ToList()
                                : new List<string>();
            var projects = projectLines.Select(line => line.Split(new[] { '=' }, 2))
                                         .Where(split => split.Length == 2)
                                         .ToDictionary(split => split[0], split => split[1]);

            var currentProject = ProjectDir + ">" + ProjectName;

            var projectDirName = projects.Where(s => string.Equals(s.Key, currentProject, StringComparison.InvariantCultureIgnoreCase))
                                           .Select(s => s.Value)
                                           .FirstOrDefault();

            if (projectDirName == null)
            {
                projectDirName = Guid.NewGuid().ToString("N");
                projectLines.Add(currentProject + "=" + projectDirName);

                FileWriteAllLines(projectListPath, projectLines);
            }

            var projectInfoDir = Path.Combine(solutionCacheDir, projectDirName);

            if (!Directory.Exists(projectInfoDir))
                Directory.CreateDirectory(projectInfoDir);

            SaveProjectInfo(projectInfoDir);
        }

        private void SaveProjectInfo(string solutionDir)
        {
            var projectInfoPath = Path.Combine(solutionDir, ProjectName + ".info");
            var assemblyFileInfo = new FileInfo(AssemblyPath);
            var razorComponentsInput = RazorComponentWithTargetPath ?? new ITaskItem[0];
            var razorComponents = string.Join(";", razorComponentsInput.Select(c => c.ItemSpec + "," + c.GetMetadata("CssScope")));
            var sources = Sources;
            var content = Content;
            var embeddedResource = EmbeddedResource;
            var ignoreFile = new IgnoreFile(Path.Combine(ProjectDir, ".livesharpignore"));
            var ignoredFiles = new List<string>();
            
            if (ignoreFile.Exists) {
                sources = ignoreFile.ProcessDelimitedFiles(sources, ignoredFiles);
                content = ignoreFile.ProcessDelimitedFiles(content, ignoredFiles);
                embeddedResource = ignoreFile.ProcessDelimitedFiles(embeddedResource, ignoredFiles);

                IgnoredFiles = string.Join(";", ignoredFiles);
            }

            var lines = new[] {
                "Sources=" + sources,
                "Content=" + content,
                "References=" + References,
                "ProjectReferences=" + ProjectReferences,
                "ProjectDir=" + ProjectDir,
                "AssemblyName=" + AssemblyName,
                "DefineConstants=" + (DefineConstants ?? ""),
                "LangVersion=" + (LangVersion ?? "latest"),
                "EmbeddedResource=" + embeddedResource,
                "AssemblyPath=" + AssemblyPath,
                "RazorComponentWithTargetPath=" + razorComponents,
                "RootNamespace=" + RootNamespace,
                "AssemblyTimestamp=" + assemblyFileInfo.LastWriteTime.ToBinary(),
                "Analyzers=" + Analyzers,
                "AnalyzerConfigFiles=" + AnalyzerConfigFiles,
                "OutputType=" + OutputType
            };

            FileWriteAllLines(projectInfoPath, lines);
        }

        private string[] FileReadAllLines(string filename)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    return File.ReadAllLines(filename);
                }
                catch (Exception e)
                {
                    _log.LogWarning(e.Message + " (retrying)");
                    Thread.Sleep(15);
                } 
            }
            throw new Exception("Unable to read " + filename);
        }

        private void FileWriteAllLines(string filename, IEnumerable<string> lines)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    File.WriteAllLines(filename, lines);
                    return;
                }
                catch (Exception e)
                {
                    _log.LogWarning(e.Message + " (retrying)");
                    Thread.Sleep(15);
                } 
            }
            throw new Exception("Unable to write " + filename);
        }
    }
}