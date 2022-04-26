using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;

namespace LiveSharp
{
    internal class NotFoundProjectItem : RazorProjectItem
    {
        /// <inheritdoc />
        public override string BasePath
        {
            get;
        }

        /// <inheritdoc />
        public override string FilePath
        {
            get;
        }

        /// <inheritdoc />
        public override string FileKind
        {
            get;
        }

        /// <inheritdoc />
        public override bool Exists => false;

        /// <inheritdoc />
        public override string PhysicalPath
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="T:Microsoft.AspNetCore.Razor.Language.NotFoundProjectItem" />.
        /// </summary>
        /// <param name="basePath">The base path.</param>
        /// <param name="path">The path.</param>
        /// <param name="fileKind">The file kind</param>
        public NotFoundProjectItem(string basePath, string path, string fileKind)
        {
            BasePath = basePath;
            FilePath = path;
            FileKind = (fileKind ?? FileKinds.GetFileKindFromFilePath(path));
        }

        /// <inheritdoc />
        public override Stream Read()
        {
            throw new NotSupportedException();
        }
    }
}