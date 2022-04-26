using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using System.Xml;

namespace LiveSharp
{

    public class DefaultRazorProjectItem : RazorProjectItem
    {
        private readonly string _fileKind;

        public FileInfo File
        {
            get;
        }

        public string OutputPath { get; }

        public override string BasePath
        {
            get;
        }

        public override string FilePath
        {
            get;
        }

        public override bool Exists => File.Exists;

        public override string PhysicalPath => File.FullName;

        public override string RelativePhysicalPath
        {
            get;
        }

        public override string CssScope { get; }

        public override string FileKind => _fileKind ?? base.FileKind;

        /// <summary>
        /// Initializes a new instance of <see cref="T:Microsoft.AspNetCore.Razor.Language.DefaultRazorProjectItem" />.
        /// </summary>
        /// <param name="basePath">The base path.</param>
        /// <param name="filePath">The path.</param>
        /// <param name="relativePhysicalPath">The physical path of the base path.</param>
        /// <param name="fileKind">The file kind. If null, the document kind will be inferred from the file extension.</param>
        /// <param name="file">The <see cref="T:System.IO.FileInfo" />.</param>
        /// <param name="outputPath"></param>
        /// <param name="cssScope"></param>
        public DefaultRazorProjectItem(string basePath, string filePath, string relativePhysicalPath, string fileKind, FileInfo file, string outputPath, string cssScope = null)
        {
            BasePath = basePath;
            FilePath = filePath;
            RelativePhysicalPath = relativePhysicalPath;
            _fileKind = fileKind;
            File = file;
            OutputPath = outputPath;
            CssScope = cssScope;
        }

        public override Stream Read()
        {
            return new FileStream(PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Write | FileShare.Delete);
        }
    }
}