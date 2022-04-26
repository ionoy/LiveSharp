using LiveSharp.Rewriters;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LiveSharp.Build
{
    class IgnoreFile
    {
        private readonly string[] _ignoreRules = new string[0];
        public bool Exists { get; }

        public IgnoreFile(string filePath)
        {
            if (File.Exists(filePath)) {
                Exists = true;
                _ignoreRules = File
                    .ReadLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToArray();
            }
        }

        public string ProcessDelimitedFiles(string fileString, List<string> ignoredFiles)
        {
            if (!Exists || fileString == null)
                return fileString;
            
            var files = fileString
                .Split(';')
                .Where(s => !string.IsNullOrWhiteSpace(s));
            
            var result = new List<string>();
            
            foreach (var file in files) {
                if (IsIgnored(file)) {
                    ignoredFiles.Add(file);
                } else {
                    result.Add(file);
                }
            }

            return string.Join(";", result);
        }

        private bool IsIgnored(string fileName)
        {
            return _ignoreRules.Any(fileName.EqualsWildcard);
        }
    }
}