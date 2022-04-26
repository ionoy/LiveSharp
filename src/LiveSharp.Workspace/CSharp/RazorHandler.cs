using LiveSharp.CSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace LiveSharp
{
    class RazorHandler
    {
        public IReadOnlyList<RazorBuildEngine> BuildEngines => _buildEngines;
        
        private readonly ConcurrentDictionary<string, string> _additionalSettings;
        private readonly ILogger _logger;
        private readonly List<RazorBuildEngine> _buildEngines = new List<RazorBuildEngine>();

        public RazorHandler(Solution solution, ConcurrentDictionary<string, string> additionalSettings,
            ILogger logger)
        {
            _additionalSettings = additionalSettings;
            _logger = logger;

            foreach (var project in solution.Projects)
                if (project.AdditionalDocuments.Any(RazorBuildEngine.IsRazorDocument))
                    _buildEngines.Add(new RazorBuildEngine(project, additionalSettings, logger));
        }

        public string GetGeneratedCode(TextDocument document)
        {
            var buildEngine = BuildEngines.FirstOrDefault(e => e.Project.FilePath == document.Project.FilePath);
            
            if (buildEngine == null)
                throw new Exception("Couldn't find RazorBuildEngine for " + document.Name + ": " + document.FilePath);
            
            return buildEngine.GetGeneratedCode(document);
        }
    }

    class StaticTagHelperFeature : ITagHelperFeature, IRazorEngineFeature, IRazorFeature
    {
        public RazorEngine Engine
        {
            get;
            set;
        }

        public IReadOnlyList<TagHelperDescriptor> TagHelpers
        {
            get;
            set;
        }

        public IReadOnlyList<TagHelperDescriptor> GetDescriptors()
        {
            return TagHelpers;
        }
    }
}