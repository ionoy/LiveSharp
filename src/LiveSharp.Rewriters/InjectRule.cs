using System;
using System.Collections.Generic;

namespace LiveSharp.Rewriters
{
    public class InjectRule
    {
        public string TypeNamePattern { get; }
        public string MethodNamePattern { get; }
        public string[] ParameterTypePatterns { get; }
        public bool NeedBaseTypeCheck { get; }

        private static readonly Dictionary<string, string> TypeAliases  = new Dictionary<string, string> {
            ["bool"] = "System.Boolean",
            ["byte"] = "System.Byte",
            ["sbyte"] =	"System.SByte", 
            ["char"] = "System.Char",
            ["decimal"] = "System.Decimal",
            ["double"] = "System.Double",
            ["float"] = "System.Single",
            ["int"] = "System.Int32",
            ["uint"] = "System.UInt32",
            ["long"] = "System.Int64",
            ["ulong"] = "System.UInt64",
            ["object"] = "System.Object",
            ["short"] = "System.Int16",
            ["ushort"] = "System.UInt16",
            ["string"] = "System.String"
        };

        public InjectRule(string typeNamePattern, string methodNamePattern = null, string[] parameterTypePatterns = null)
        {
            if (typeNamePattern.EndsWith("!")) {
                TypeNamePattern = typeNamePattern.Substring(0, typeNamePattern.Length - 1);
                NeedBaseTypeCheck = false;
            } else {
                TypeNamePattern = typeNamePattern;
                NeedBaseTypeCheck = true;
            }

            MethodNamePattern = methodNamePattern;
            ParameterTypePatterns = parameterTypePatterns;
        }

        public bool MatchesParameters(string[] parameterTypeNames)
        {
            if (ParameterTypePatterns == null)
                return true;

            // if parameter type names are not provided then match is successful
            if (parameterTypeNames != null) {
                for (int i = 0; i < parameterTypeNames.Length; i++) {
                    if (i < ParameterTypePatterns.Length) {
                        var parameterTypePattern = ParameterTypePatterns[i];

                        if (TypeAliases.TryGetValue(parameterTypePattern, out var parameterFullTypeName))
                            parameterTypePattern = parameterFullTypeName;

                        if (!MatchPart(parameterTypeNames[i], parameterTypePattern))
                            return false;
                    }
                }
            }

            return true;
        }

        public bool MatchesMethod(string methodName)
        {
            return MatchPart(methodName, MethodNamePattern);
        }

        public bool MatchesType(string fullTypeName)
        {
            return MatchPart(fullTypeName, TypeNamePattern);
        }

        private bool MatchPart(string matchedPart, string pattern)
        {
            // Cecil describes nested types as A.B/C, while SR defines it as A.B+C
            matchedPart = matchedPart.Replace('/', '+');

            // if pattern is not provided then match is successful
            if (pattern == null || pattern == "*")
                return true;

            return matchedPart.EqualsWildcard(pattern);
        }
    }
}
