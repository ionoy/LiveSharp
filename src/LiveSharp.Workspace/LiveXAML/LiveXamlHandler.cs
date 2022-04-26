using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using LiveSharp.Infrastructure;
using LiveSharp.ServerClient;
using LiveSharp.Shared.Network;
using LiveSharp.VisualStudio.LiveXAML;
using Microsoft.CodeAnalysis;

namespace LiveSharp.LiveXAML
{
    class LiveXamlHandler
    {
        private readonly InitialPropertiesContainer _initialPropertiesContainer;
        private readonly LiveSharpWorkspace _liveSharpWorkspace;
        private readonly ILogger _logger;

        public LiveXamlHandler(LiveSharpWorkspace workspace, ILogger logger)
        {
            _liveSharpWorkspace = workspace;
            _logger = logger;

            _initialPropertiesContainer = new InitialPropertiesContainer(logger);
        }

        internal async Task FileChangedAsync(string absolutePath, string relativePath)
        {
            try {
                if (!absolutePath.EndsWith(".xaml", StringComparison.InvariantCultureIgnoreCase) &&
                    !absolutePath.EndsWith(".css", StringComparison.InvariantCultureIgnoreCase))
                    return;

                var (success, xamlText) = await FSUtils.ReadFileTextAsync(absolutePath);
                if (!success)
                {
                    _logger.LogError("Couldn't read the updated file");
                    return;
                }
                
                var propertiesToReset = new string[0];
                
                if (absolutePath.EndsWith(".xaml", StringComparison.InvariantCultureIgnoreCase)) {
                    propertiesToReset = _initialPropertiesContainer.GetPropertiesForFile(absolutePath);
                    _initialPropertiesContainer.AddFile(absolutePath, xamlText);
                }

                _logger.LogMessage("Document saved " + absolutePath);

                var buffer = GetBuffer(absolutePath, relativePath, propertiesToReset, xamlText);

                _liveSharpWorkspace.SendBroadcast(buffer, ContentTypes.LiveXaml.XamlUpdate, BroadcastGroups.LiveXaml);
            } catch (Exception e) {
                _logger.LogMessage("Unable to send updates: " + e);
            }
        }

        public void ReadInitialPropertiesOfXamlFiles(Microsoft.CodeAnalysis.Workspace workspace)
        {
            Task.Run(async () => {
                try {
                    var xamlFiles = workspace.CurrentSolution
                        .Projects
                        .SelectMany(p => p.AdditionalDocuments)
                        .Where(d => string.Equals(Path.GetExtension(d.FilePath), ".xaml", StringComparison.InvariantCultureIgnoreCase));

                    foreach (var xamlFile in xamlFiles)
                    {
                        var source = await xamlFile.GetTextAsync();
                        
                        _initialPropertiesContainer.AddFile(xamlFile.FilePath, source.ToString());
                    }
                } catch (Exception e) {
                    _logger.LogError("Reading initial properties failed: " + e);
                }
            });
        }

        private static byte[] GetBuffer(string filePath, string relativeFilename,
            string[] propertiesToReset, string xamlText)
        {
            var header = new byte[] { 0xbe, 0xef };
            var footer = new byte[] { 0xff };
            var result = header.ToList();

            result.Add(1);
            
            byte[] buffer;

            var extension = Path.GetExtension(filePath);
            if (extension != null && 
                (extension.Equals(".xaml", StringComparison.InvariantCultureIgnoreCase) || 
                 extension.Equals(".css", StringComparison.InvariantCultureIgnoreCase)))
                buffer = Encoding.Unicode.GetBytes(xamlText);
            else
                buffer = File.ReadAllBytes(filePath);
            
            if (extension != null && extension.Equals(".xaml", StringComparison.InvariantCultureIgnoreCase)) {
                try {
                    var doc = XDocument.Parse(Encoding.Unicode.GetString(buffer));
                    var targetId = doc.Root.Attributes()
                        .Where(a => a.Name.LocalName == "Class" && a.Name.Namespace == doc.Root.GetNamespaceOfPrefix("x"))
                        .Select(a => a.Value)
                        .FirstOrDefault();

                    if (targetId == null)
                        targetId = relativeFilename.Replace("\\", "/");

                    result.AddRange(CreateMarkupBuffer(buffer, targetId));
                }
                catch (Exception) {
                    throw new MalformedXamlException();
                }
            } else if (extension != null && extension.Equals(".css", StringComparison.InvariantCultureIgnoreCase)) {
                var targetId = relativeFilename.Replace("\\", "/");
                result.AddRange(CreateMarkupBuffer(buffer, targetId));
            }

            var propertyList = GetPropertiesToReset(propertiesToReset);
            var propertiesBuffer = CreatePropertiesBuffer(propertyList);

            result.AddRange(propertiesBuffer);
            result.AddRange(footer);
            
            return result.ToArray();
        }

        private static byte[] CreatePropertiesBuffer(List<string> properties)
        {
            var propertiesString = string.Join(",", properties);
            var buffer = Encoding.Unicode.GetBytes(propertiesString);
            var bufferLen = BitConverter.GetBytes((ushort)buffer.Length);

            return bufferLen.Concat(buffer).ToArray();
        }

        private static byte[] CreateMarkupBuffer(byte[] buffer, string targetId)
        {
            var targetIdBuffer = Encoding.Unicode.GetBytes(targetId);
            var targetIdLength = BitConverter.GetBytes(targetIdBuffer.Length);
            var length = BitConverter.GetBytes(buffer.Length);
            var checksum = BitConverter.GetBytes(Fletcher16(buffer));

            return targetIdLength.Concat(targetIdBuffer)
                                 .Concat(length)
                                 .Concat(buffer)
                                 .Concat(checksum)
                                 .ToArray();
        }

        private static ushort Fletcher16(byte[] data)
        {
            ushort sum1 = 0;
            ushort sum2 = 0;

            for (var index = 0; index < data.Length; ++index) {
                sum1 = (ushort)((sum1 + data[index]) % 255);
                sum2 = (ushort)((sum2 + sum1) % 255);
            }

            return (ushort)((sum2 << 8) | sum1);
        }

        private static List<string> GetPropertiesToReset(string[] propertiesToReset)
        {
            if (propertiesToReset == null)
                return new List<string>();

            return propertiesToReset.Distinct()
                                    .ToList();
        }
    }
}
