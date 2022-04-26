using LiveSharp.ServerClient;
using System.Collections.Concurrent;
using System.Linq;
using System.Xml.Linq;

namespace LiveSharp.VisualStudio.LiveXAML
{
    class InitialPropertiesContainer
    {
        ConcurrentDictionary<string, string[]> _initialPropertiesByFile = new ConcurrentDictionary<string, string[]>();
        private ILogger _logger;

        public InitialPropertiesContainer(ILogger logger)
        {
            _logger = logger;
        }

        public void AddFile(string filepath, string xamlText)
        {
            var doc = XDocument.Parse(xamlText);
            if (doc.Root == null)
                return;
            
            var properties = doc.Root
                                .Attributes()
                                .Where(a => !a.IsNamespaceDeclaration && a.Name.NamespaceName == "")
                                .Select(a => a.Name.LocalName)
                                .ToArray();

            _initialPropertiesByFile[NormalizePath(filepath)] = properties;
        }

        public string[] GetPropertiesForFile(string filepath)
        {
            if (_initialPropertiesByFile.TryGetValue(NormalizePath(filepath), out var properties))
                return properties;

            _logger.LogError("GetPropertiesForFile couldn't find file: " + filepath);

            return new string[0];
        }

        // Probably will need to do more, like check for / vs \ and other stuff. 
        private static string NormalizePath(string filepath)
        {
            return filepath.ToLower();
        }
    }
}
