using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace LiveSharp
{
    internal class CompositeRazorProjectFileSystem : RazorProjectFileSystem
    {
        public IReadOnlyList<RazorProjectFileSystem> FileSystems
        {
            get;
        }

        public CompositeRazorProjectFileSystem(IReadOnlyList<RazorProjectFileSystem> fileSystems)
        {
            FileSystems = (fileSystems ?? throw new ArgumentNullException("fileSystems"));
        }

        public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath)
        {
            foreach (RazorProjectFileSystem fileSystem in FileSystems)
            {
                foreach (RazorProjectItem item in fileSystem.EnumerateItems(basePath))
                {
                    yield return item;
                }
            }
        }

        [Obsolete("Use GetItem(string path, string fileKind) instead.")]
        public override RazorProjectItem GetItem(string path)
        {
            return GetItem(path, null);
        }

        public override RazorProjectItem GetItem(string path, string fileKind)
        {
            RazorProjectItem razorProjectItem = null;
            foreach (RazorProjectFileSystem fileSystem in FileSystems)
            {
                razorProjectItem = fileSystem.GetItem(path, fileKind);
                if (razorProjectItem != null && razorProjectItem.Exists)
                {
                    return razorProjectItem;
                }
            }
            return razorProjectItem;
        }
    }
}