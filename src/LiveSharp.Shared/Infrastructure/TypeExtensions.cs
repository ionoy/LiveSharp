using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSharp.Shared.Infrastructure
{
    public static class TypeExtensions
    {
        private static readonly Dictionary<Type, string> Aliases =
            new Dictionary<Type, string>()
            {
                { typeof(byte), "byte" },
                { typeof(sbyte), "sbyte" },
                { typeof(short), "short" },
                { typeof(ushort), "ushort" },
                { typeof(int), "int" },
                { typeof(uint), "uint" },
                { typeof(long), "long" },
                { typeof(ulong), "ulong" },
                { typeof(float), "float" },
                { typeof(double), "double" },
                { typeof(decimal), "decimal" },
                { typeof(object), "object" },
                { typeof(bool), "bool" },
                { typeof(char), "char" },
                { typeof(string), "string" },
                { typeof(void), "void" }
            };
        
        internal static string GetTypeName(this Type type)
        {
            if (type.IsConstructedGenericType) {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                var genericCountIndex = genericTypeDefinition.Name.IndexOf('`');
                if (genericCountIndex == -1)
                    genericCountIndex = genericTypeDefinition.Name.Length;
                var typeDefinitionName = genericTypeDefinition.Name.Substring(0, genericCountIndex);
                var genericTypeArguments = string.Join(", ", type.GenericTypeArguments.Select(t => GetTypeName(t)));
                return typeDefinitionName + "<" + genericTypeArguments + ">";
            } 
            
            if (type.IsArray) {
                return GetTypeName(type.GetElementType()) + "[]";
            }
            
            if (Aliases.TryGetValue(type, out var alias))
                return alias;
            
            return type.Name;
        }
    }
}