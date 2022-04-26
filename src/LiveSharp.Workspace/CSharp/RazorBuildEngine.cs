using LiveSharp.Infrastructure;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Razor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LiveSharp.CSharp
{
    public class RazorBuildEngine
    {
        public Project Project { get; }

        private readonly ConcurrentDictionary<string, string> _additionalSettings;
        private readonly ILogger _logger;

        private readonly SourceItem[] _sourceItems;
 
        public RazorProjectEngine Engine { get; }
        public VirtualRazorProjectFileSystem FileSystem { get; }

        public RazorBuildEngine(Project project, ConcurrentDictionary<string, string> additionalSettings,
            ILogger logger)
        {
            Project = project;

            _additionalSettings = additionalSettings;
            _logger = logger;
                
            var razorDocuments = project.AdditionalDocuments.Where(IsRazorDocument).ToArray();
            var sources = razorDocuments.Select(d => d.FilePath).ToList();
            var outputs = sources.Select(s => s + ".g.cs").ToList();
            var relativePaths = razorDocuments.Select(d => d.Name).ToList();
            var kinds = razorDocuments.Select(d => "component").ToList();
            var rootDirectoryPath = project.FilePath.TrimEnd('\\');
            
            _sourceItems = GetSourceItems(rootDirectoryPath, sources, outputs, relativePaths, kinds);
            
            //var extension = new AssemblyExtension("MVC-3.0", Assembly.LoadFrom(@"C:\Program Files\dotnet\sdk\3.1.102\Sdks\Microsoft.NET.Sdk.Razor\extensions\mvc-3-0\Microsoft.AspNetCore.Mvc.Razor.Extensions.dll"));
            var razorConfiguration = RazorConfiguration.Create(RazorLanguageVersion.Version_6_0, "MVC-6.0", new RazorExtension[0]);
            var virtualRazorProjectFileSystem = GetVirtualRazorProjectSystem(_sourceItems);
            var fileSystem = new CompositeRazorProjectFileSystem(new[]
            {
                virtualRazorProjectFileSystem,
                RazorProjectFileSystem.Create(rootDirectoryPath)
            });
                
            var discoverEngine = RazorProjectEngine.Create(razorConfiguration, fileSystem, builder =>
            {
                var self = MetadataReference.CreateFromFile(Path.Combine(project.FilePath, project.OutputFilePath));
                var references = project.MetadataReferences.Concat(new [] {self}).ToArray();
                    
                builder.Features.Add(new DefaultMetadataReferenceFeature { References = references });
                builder.Features.Add(new CompilationTagHelperFeature());
                builder.Features.Add(new DefaultTagHelperDescriptorProvider());
                    
                CompilerFeatures.Register(builder);
            });
                
            var descriptors = discoverEngine.Engine.Features.OfType<ITagHelperFeature>().Single().GetDescriptors();
            var rootNamespace = project.Name;
            
            if (WorkspaceLoader.ProjectProperties.TryGetValue(project.Name, out var props))
                props.TryGetValue("RootNamespace", out rootNamespace);

            FileSystem = virtualRazorProjectFileSystem;
            
            Engine = RazorProjectEngine.Create(razorConfiguration, fileSystem, builder =>
            {
                builder.Features.Add(new StaticTagHelperFeature
                {
                    TagHelpers = descriptors
                });
                builder.Features.Add(GetDefaultTypeNameFeature());
                builder.Features.Add(new CompilationTagHelperFeature());
                    
                builder.SetRootNamespace(rootNamespace);
                builder.SetCSharpLanguageVersion(LanguageVersion.CSharp10);
                    
                CompilerFeatures.Register(builder);
            });
        }
        
        private VirtualRazorProjectFileSystem GetVirtualRazorProjectSystem(IReadOnlyList<SourceItem> inputItems)
        {
            var virtualRazorProjectFileSystem = new VirtualRazorProjectFileSystem();
            
            for (int i = 0; i < inputItems.Count; i++)
            {
                var sourceItem = inputItems[i];

                var cssScope = _additionalSettings.TryGetValue(sourceItem.RelativePhysicalPath, out var scope) ? scope : null;
                var projectItem = new DefaultRazorProjectItem("/", sourceItem.FilePath, sourceItem.RelativePhysicalPath, sourceItem.FileKind, new FileInfo(sourceItem.SourcePath), sourceItem.OutputPath, cssScope);
                
                virtualRazorProjectFileSystem.Add(projectItem);
            }
            
            return virtualRazorProjectFileSystem;
        }
        
        private SourceItem[] GetSourceItems(string projectDirectory, List<string> sources, List<string> outputs, List<string> relativePath, List<string> fileKinds)
        {
            var array = new SourceItem[sources.Count];
            
            for (int i = 0; i < array.Length; i++)
            {
                Path.Combine(projectDirectory, outputs[i]);
                string fileKind = (fileKinds.Count > 0) ? fileKinds[i] : "mvc";
                if (FileKinds.IsComponent(fileKind)) 
                    fileKind = FileKinds.GetComponentFileKindFromFilePath(sources[i]);
                
                array[i] = new SourceItem(sources[i], outputs[i], relativePath[i], fileKind);
            }
            
            return array;
        }

        private static RazorEngineFeatureBase GetDefaultTypeNameFeature()
        {
            var defaultTypeNameFeatureType = typeof(DefaultMetadataReferenceFeature).Assembly.GetTypes().FirstOrDefault(t =>
                t.FullName == "Microsoft.CodeAnalysis.Razor.DefaultTypeNameFeature");
            var defaultTypeNameFeature = (RazorEngineFeatureBase) Activator.CreateInstance(defaultTypeNameFeatureType);
            return defaultTypeNameFeature;
        }

        public string GetGeneratedCode(TextDocument document)
        {
            var sourceItem = _sourceItems.FirstOrDefault(i => i.SourcePath == document.FilePath);
            var razorItem = Engine.FileSystem.GetItem(sourceItem.FilePath, sourceItem.FileKind);
            return GetGeneratedCode(razorItem);
        }
        
        public string GetGeneratedCode(RazorProjectItem razorItem)
        {
            var result = Engine.Process(razorItem);
            var razorCSharpDocument = result.GetCSharpDocument();

            foreach (var diagnostic in razorCSharpDocument.Diagnostics.Where(d => d.Severity == RazorDiagnosticSeverity.Error))
                _logger.LogWarning("Razor compilation error: " + diagnostic);

            return razorCSharpDocument.GeneratedCode;
        }

        public static bool IsRazorDocument(TextDocument document)
        {
            var extension = Path.GetExtension(document.FilePath);
            return string.Equals(extension, ".razor", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}