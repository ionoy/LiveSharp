using System.IO;

namespace LiveSharp
{
    struct SourceItem
    {
        public string SourcePath
        {
            get;
        }

        public string OutputPath
        {
            get;
        }

        public string RelativePhysicalPath
        {
            get;
        }

        public string FilePath
        {
            get;
        }

        public string FileKind
        {
            get;
        }

        public SourceItem(string sourcePath, string outputPath, string physicalRelativePath, string fileKind)
        {
            SourcePath = sourcePath;
            OutputPath = outputPath;
            RelativePhysicalPath = physicalRelativePath;
            FilePath = "/" + physicalRelativePath.Replace(Path.DirectorySeparatorChar, '/').Replace("//", "/");
            FileKind = fileKind;
        }
    }
}