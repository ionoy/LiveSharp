using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LiveSharp.VisualStudio.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace LiveSharp.VisualStudio.Infrastructure
{
    public static class Utils
    {
        public static string GetImageFullPath(string filename)
        {
            return Path.Combine(
                //Get the location of your package dll
                Assembly.GetExecutingAssembly().Location,
                //reference your 'images' folder
                "/Resources/",
                filename
            );
        }

        public static async Task<IMethodSymbol> GetMethodAtPositionAsync(this Solution solution, string filename, int position)
        {
            var doc = solution.Projects.SelectMany(p => p.Documents).FirstOrDefault(d => string.Equals(d.FilePath, filename, StringComparison.InvariantCultureIgnoreCase));

            if (doc == null)
                return null;

            var semanticModel = await doc.GetSemanticModelAsync();
            var root = await doc.GetSyntaxRootAsync();
            var method = root.DescendantNodes()
                             .OfType<MethodDeclarationSyntax>()
                             .FirstOrDefault(md => md.Identifier.Span.Contains(position));

            if (method != null)
                return semanticModel.GetDeclaredSymbol(method);

            var ctor = root.DescendantNodes()
                           .OfType<ConstructorDeclarationSyntax>()
                           .FirstOrDefault(md => md.Identifier.Span.Contains(position));

            if (ctor != null)
                return semanticModel.GetDeclaredSymbol(ctor);

            return null;
        }

        public static string MakeRelativePath(this string fromPath, string toPath)
        {
            if (string.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
            if (string.IsNullOrEmpty(toPath))   throw new ArgumentNullException("toPath");

            var fromUri = new Uri(fromPath);
            var toUri = new Uri(toPath);

            if (fromUri.Scheme != toUri.Scheme) { return toPath; } // path can't be made relative.

            var relativeUri = fromUri.MakeRelativeUri(toUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

            if (toUri.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }

            return relativePath;
        }
    }
}
