using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;

namespace LiveSharp
{
    public class VirtualRazorProjectFileSystem : RazorProjectFileSystem
    {
        [DebuggerDisplay("{Path}")]
        public class DirectoryNode
        {
            public string Path
            {
                get;
            }

            public List<DirectoryNode> Directories
            {
                get;
            } = new List<DirectoryNode>();


            public List<FileNode> Files
            {
                get;
            } = new List<FileNode>();


            public DirectoryNode(string path)
            {
                Path = path;
            }

            public void AddFile(FileNode fileNode)
            {
                string path = fileNode.Path;
                if (!path.StartsWith(Path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"File doesn't belong to path {fileNode.Path}, {Path}");
                
                string directoryPath = GetDirectoryPath(path);
                GetOrAddDirectory(this, directoryPath, createIfNotExists: true).Files.Add(fileNode);
            }

            public DirectoryNode GetDirectory(string path)
            {
                if (!path.StartsWith(Path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"File doesn't belong to directory {path}, {Path}");
                
                return GetOrAddDirectory(this, path);
            }

            public IEnumerable<RazorProjectItem> EnumerateItems()
            {
                foreach (FileNode file in Files)
                {
                    yield return file.ProjectItem;
                }
                foreach (DirectoryNode directory in Directories)
                {
                    foreach (RazorProjectItem item in directory.EnumerateItems())
                    {
                        yield return item;
                    }
                }
            }

            public RazorProjectItem GetItem(string path)
            {
                if (!path.StartsWith(Path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"File doesn't belong to directory {path}, {Path}");
                
                string directoryPath = GetDirectoryPath(path);
                DirectoryNode orAddDirectory = GetOrAddDirectory(this, directoryPath);
                if (orAddDirectory == null)
                {
                    return null;
                }
                foreach (FileNode file in orAddDirectory.Files)
                {
                    string path2 = file.Path;
                    int length = orAddDirectory.Path.Length;
                    if (string.Compare(path, length, path2, length, path.Length - length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return file.ProjectItem;
                    }
                }
                return null;
            }

            private static string GetDirectoryPath(string path)
            {
                int num = path.LastIndexOf('/');
                if (num == -1)
                {
                    return path;
                }
                return path.Substring(0, num + 1);
            }

            private static DirectoryNode GetOrAddDirectory(DirectoryNode directory, string path, bool createIfNotExists = false)
            {
                if (path[path.Length - 1] != '/')
                {
                    path += "/";
                }
                int num;
                while ((num = path.IndexOf('/', directory.Path.Length)) != -1 && num != path.Length)
                {
                    DirectoryNode directoryNode = FindSubDirectory(directory, path);
                    if (directoryNode == null)
                    {
                        if (!createIfNotExists)
                        {
                            return null;
                        }
                        directoryNode = new DirectoryNode(path.Substring(0, num + 1));
                        directory.Directories.Add(directoryNode);
                    }
                    directory = directoryNode;
                }
                return directory;
            }

            private static DirectoryNode FindSubDirectory(DirectoryNode parentDirectory, string path)
            {
                for (int i = 0; i < parentDirectory.Directories.Count; i++)
                {
                    DirectoryNode directoryNode = parentDirectory.Directories[i];
                    string path2 = directoryNode.Path;
                    int length = parentDirectory.Path.Length;
                    _ = path2.Length;
                    if (string.Compare(path, length, path2, length, path2.Length - length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return directoryNode;
                    }
                }
                return null;
            }
        }

        [DebuggerDisplay("{Path}")]
        public struct FileNode
        {
            public string Path
            {
                get;
            }

            public DefaultRazorProjectItem ProjectItem
            {
                get;
            }

            public FileNode(string path, DefaultRazorProjectItem projectItem)
            {
                Path = path;
                ProjectItem = projectItem;
            }
        }

        public DirectoryNode Root { get; } = new DirectoryNode("/");

        public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath)
        {
            basePath = NormalizeAndEnsureValidPath(basePath);
            return Root.GetDirectory(basePath)?.EnumerateItems() ?? Enumerable.Empty<RazorProjectItem>();
        }

        [Obsolete("Use GetItem(string path, string fileKind) instead.")]
        public override RazorProjectItem GetItem(string path)
        {
            return GetItem(path, null);
        }

        public override RazorProjectItem GetItem(string path, string fileKind)
        {
            path = NormalizeAndEnsureValidPath(path);
            return Root.GetItem(path) ?? new NotFoundProjectItem(string.Empty, path, fileKind);
        }

        public void Add(DefaultRazorProjectItem projectItem)
        {
            if (projectItem == null)
            {
                throw new ArgumentNullException("projectItem");
            }
            string path = NormalizeAndEnsureValidPath(projectItem.FilePath);
            Root.AddFile(new FileNode(path, projectItem));
        }
    }
}