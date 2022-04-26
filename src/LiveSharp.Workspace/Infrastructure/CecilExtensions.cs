using System.Linq;
using System.Runtime.CompilerServices;
using LiveSharp.VisualStudio.Services;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace LiveSharp.Infrastructure
{
    public static class CecilExtensions
    {
        public static string GetMethodIdentifier(this MethodDefinition methodDefinition, bool includeTypeName)
        {
            var declaringTypeName = methodDefinition.DeclaringType.FullName.Replace("/", "+");
            var typeNameArray = methodDefinition.Parameters.Select(p => p.ParameterType.GetFullName())
                .ToArray();
            var typeNames = string.Join(" ", typeNameArray);
            
            if (includeTypeName)
                return $"{declaringTypeName} {methodDefinition.Name} {typeNames}";
            return $"{methodDefinition.Name} {typeNames}";
        }

        public static string GetFullName(this TypeReference type)
        {
            return type.FullName.Replace("/", "+")
                .Replace("<", "[")
                .Replace(">", "]");
        }

        public static bool IsCompilerGenerated(this ICustomAttributeProvider attributeProvider)
        {
            var customAttributes = attributeProvider.CustomAttributes;
            return customAttributes.Any(ca => ca.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName);
        }
        
        public static IEnumerable<TypeDefinition> GetAllTypes(this AssemblyDefinition assembly)
        {
            foreach (var type in assembly.MainModule.Types.SelectMany(walkTypes)) {
                yield return type;
            }

            IEnumerable<TypeDefinition> walkTypes(TypeDefinition type)
            {
                yield return type;

                foreach (var nestedType in type.NestedTypes.SelectMany(walkTypes))
                    yield return nestedType;
            }
        }
    }
}