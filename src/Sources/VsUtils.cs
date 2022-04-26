using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LiveSharp.VisualStudio;

namespace LiveProjectCommon
{
    public static class VsUtils
    {
        public const string vsProjectKindSolutionFolder = "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}";

        public static IEnumerable<string> EnumerateSolutionFiles(Solution solution)
        {
            return GetAllProjects(solution).SelectMany(p => EnumerateProjectFiles(p))
                                           .Select(pi => GetProjectItemFilename(pi));
        }

        public static IEnumerable<ProjectItem> EnumerateProjectItems(Solution solution)
        {
            return GetAllProjects(solution).SelectMany(p => EnumerateProjectFiles(p));
        }

        public static IEnumerable<ProjectItem> EnumerateProjectFiles(Project project)
        {
            IEnumerable<ProjectItem> GetChildItemsAndSelf(ProjectItem pi) {
                ProjectItems children = null;
                try {
                    children = pi.ProjectItems;
                } catch {}

                if (children != null)
                    foreach (ProjectItem child in children)
                        foreach (var subChildItem in GetChildItemsAndSelf(child))
                            yield return subChildItem;
                
                yield return pi;
            }

            try {
                if (project.ProjectItems == null)
                    yield break;
            } catch {}
            
            var allProjectItems = project.ProjectItems
                                         .OfType<ProjectItem>()
                                         .SelectMany(pi => GetChildItemsAndSelf(pi));
            
            foreach (var pi in allProjectItems)
                yield return pi;
        }

        public static string GetProjectItemFilename(this ProjectItem pi)
        {
            try {
                if (pi.FileCount > 0)
                    return pi.FileNames[0];

                return null;
            } catch {
                return null;
            }
        }

        public static IReadOnlyList<Project> GetAllProjects(Solution solution)
        {
            var result = new List<Project>();
            return GetAllProjects(solution.Projects.OfType<Project>(), result);
        }

        public static IReadOnlyList<Project> GetAllProjects(IEnumerable<Project> projects, List<Project> result)
        {
            foreach (var proj in projects)
            {
                if (proj.Kind == vsProjectKindSolutionFolder)
                {
                    var folderProjects = proj.ProjectItems
                                             .OfType<ProjectItem>()
                                             .Select(pi => pi.SubProject)
                                             .OfType<Project>();

                    GetAllProjects(folderProjects, result);
                }
                else
                {
                    result.Add(proj);
                }
            }

            return result;
        }
        private static readonly ConcurrentDictionary<string, Project> ProjectCache = new ConcurrentDictionary<string, Project>();

        public static ProjectItem GetProjectItemFromFilename(DTE dte, string filename)
        {
            try {
                return dte.Solution.Projects.OfType<Project>()
                          .SelectMany(p => GetFileList(p))
                          .FirstOrDefault(pi => GetFilename(pi).Equals(filename, StringComparison.InvariantCultureIgnoreCase));
            } catch {
                return null;
            }
        }

        public static ProjectItem[] GetFileList(Project genericProject)
        {
            return GetProjectItems(genericProject.ProjectItems).ToArray();
        }

        public static IEnumerable<ProjectItem> GetProjectItems(ProjectItems root)
        {
            foreach (var pi in root) {
                var projectItem = (ProjectItem)pi;
                var children = projectItem.ProjectItems;

                if (children != null)
                    foreach (var childItem in GetProjectItems(children))
                        yield return childItem;

                yield return projectItem;
            }
        }

        public static string GetFilename(ProjectItem pi)
        {
            try {
                return pi.FileNames[0];
            } catch (Exception) {
                return "";
            }
        }

        public static Project GetProjectByFilename(DTE dte, string filename)
        {
            return GetProjectFromCache(filename, _ => {
                if (dte != null) {
                    var projectItem = dte.Solution.FindProjectItem(filename);
                    return projectItem?.ContainingProject;
                }

                return null;
            });
        }

        public static Project GetProjectByName(DTE dte, string projectName)
        {
            return GetProjectFromCache(projectName, _ => {
                if (dte != null) {
                    foreach (var p in dte.Solution.Projects) {
                        var project = (Project)p;
                        if (project.Name.Equals(projectName, StringComparison.InvariantCultureIgnoreCase))
                            return project;
                    }
                }

                return null;
            });
        }

        private static Project GetProjectFromCache(string key, Func<string, Project> resolver)
        {
            Project project;
            if (ProjectCache.TryGetValue(key, out project)) {
                try {
                    Logger.WriteLine(project.Name); // Check that project is available
                    return project;
                } catch {
                    // Update cache if not available 
                    return ProjectCache[key] = resolver(key);
                }
            } else {
                return ProjectCache[key] = resolver(key);
            }
        }
    }
}
