using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using System.Globalization;

namespace LiveSharp.Ide.Infrastructure
{
    public static class RoslynExtensions
    {
        public static string GetMethodIdentifier(this IMethodSymbol ms, bool includeTypeName = true)
        {
            var methodName = ms.Name;
            var parameters = string.Join(" ", ms.Parameters.Select(p => p.Type.GetFullyQualifedName()));

            if (includeTypeName)
                return GetMethodIdentifier(ms.ContainingType, methodName, parameters);
            else
                return $"{methodName} {parameters}";
        }
        public static string GetPropertyAccessorIdentifier(this IPropertySymbol prop, bool isGet = true)
        {
            var prefix = isGet ? "get_" : "set_";
            var methodName = prefix + prop.Name;
            var parameter = isGet ? "" : prop.Type.GetFullyQualifedName();
            
            return GetMethodIdentifier(prop.ContainingType, methodName, parameter, false);
        }

        public static string GetMethodIdentifier(INamedTypeSymbol containingType, string methodName, string parameters, bool includeTypeName = true)
        {
            if (includeTypeName) {
                var typeName = containingType.GetFullyQualifedName();
                return $"{typeName} {methodName} {parameters}";
            } else {
                return $"{methodName} {parameters}";
            }
        }

        public static string GetFullyQualifedName(this ITypeSymbol ts, bool includeGenericTypeParameters = true)
        {
            if (ts is IArrayTypeSymbol ats) {
                return ats.ElementType.GetFullyQualifedName(includeGenericTypeParameters) + "[]";
            } else {
                return GetFullMetadataName(ts, includeGenericTypeParameters);
            }
        }

        public static bool Is(this ITypeSymbol ts, Type type)
        {
            return ts.GetFullyQualifedName() == type.ToString();
        }

        public static bool Is(this ITypeSymbol left, ITypeSymbol right)
        {
            return left.GetFullyQualifedName() == right.GetFullyQualifedName();
        }

        public static string GetFullMetadataName(this INamespaceOrTypeSymbol s, bool includeGenericTypeParameters = true)
        {
            if (s == null || IsRootNamespace(s))
                return string.Empty;

            var sb = new StringBuilder(s.MetadataName);
            var last = s;

            if (includeGenericTypeParameters && s is INamedTypeSymbol its && its.TypeArguments.Length > 0) {
                var args = string.Join(",", its.TypeArguments.Select(t => t.GetFullyQualifedName()));
                sb.Append("[" + args + "]");
            }

            var nsOrType = s.ContainingSymbol;
            while (!(nsOrType is INamespaceOrTypeSymbol) && nsOrType != null)
                nsOrType = nsOrType.ContainingSymbol;

            s = nsOrType as INamespaceOrTypeSymbol;

            while (!IsRootNamespace(s)) {
                if (s is ITypeSymbol && last is ITypeSymbol) sb.Insert(0, '+');
                else sb.Insert(0, '.');

                sb.Insert(0, s.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                s = (INamespaceOrTypeSymbol)s.ContainingSymbol;
            }

            return sb.ToString();
        }

        private static bool IsRootNamespace(ISymbol symbol)
        {
            INamespaceSymbol s = null;
            return (s = symbol as INamespaceSymbol) != null && s.IsGlobalNamespace;
        }

        public static IReadOnlyList<ITypeSymbol> GetBaseTypes(this ITypeSymbol ts)
        {
            var types = new List<ITypeSymbol>();
            var type = ts;

            while ((type = type.BaseType) != null)
                types.Add(type);

            return types;
        }

        public static bool IsNullable(this ITypeSymbol type)
        {
            if (type is INamedTypeSymbol ints)
                return ints.ConstructedFrom?.GetFullyQualifedName(false) == typeof(Nullable<>).FullName;
            return typeof(Nullable<>).ToString() == type.GetFullyQualifedName();
        }

        public static ITypeSymbol GetArrayElementType(this ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol arr)
                return arr.ElementType;
            throw new InvalidOperationException(type + " is not an array type");
        }
        public static ITypeSymbol GetInputType(this IPatternOperation patternOperation)
        {
            return (ITypeSymbol)patternOperation.GetType()
                                         .GetProperty("InputType", BindingFlags.Instance | BindingFlags.Public)
                                         .GetValue(patternOperation);
        }

        public static bool Implements(this ITypeSymbol typeSymbol, string fullInterfaceName)
        {
            do {
                if (typeSymbol.Interfaces.Any(i => i.GetFullMetadataName() == fullInterfaceName))
                    return true;
                typeSymbol = typeSymbol.BaseType;
            } while (typeSymbol != null);

            return false;
        } 
        
        public static bool TryFindRazorGeneratedDocument(this Project project, string razorFileName, out DocumentId documentId, out string normalizedGeneratedFileName)
        {
            var results = new List<Document>();
            var directorySeparatorChar = Path.DirectorySeparatorChar;
            var alternativeSeparatorChar = directorySeparatorChar == '/' ? '\\' : '/';
            
            razorFileName = razorFileName.Replace(alternativeSeparatorChar, directorySeparatorChar);
            
            normalizedGeneratedFileName = razorFileName + ".g.cs";

            foreach (var document in project.Documents) {
                var documentFilePath = normalizePath(document.FilePath, UriKind.Absolute);
                if (documentFilePath.EndsWith(normalizedGeneratedFileName, true, CultureInfo.InvariantCulture))
                    results.Add(document);
            }

            if (results.Count > 0) {
                foreach (var document in results) {
                    if (document.TryGetText(out var text) && text.Lines.Count > 0) {
                        var lineText = text.Lines[0].Text.ToString();
                        if (lineText.IndexOf(razorFileName, StringComparison.InvariantCultureIgnoreCase) != -1) {
                            documentId = document.Id;
                            return true;
                        }
                    }
                }
            }

            if (results.Count > 0) {
                documentId = results[0].Id;
                return true;
            }

            documentId = null;

            return false;
            
            string normalizePath(string path, UriKind kind) =>
                Path.GetFullPath(new Uri(path, kind).LocalPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .ToUpperInvariant();
        }
    }
}
