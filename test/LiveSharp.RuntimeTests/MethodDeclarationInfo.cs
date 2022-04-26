using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LiveSharp.RuntimeTests
{
    class MethodDeclarationInfo
    {
        public ITypeSymbol ReturnType { get; }
        public SyntaxNode Body { get; }
        public IReadOnlyList<ParameterSyntax> Parameters { get; }

        public MethodDeclarationInfo(ITypeSymbol returnType, SyntaxNode body, IReadOnlyList<ParameterSyntax> parameters)
        {
            ReturnType = returnType;
            Body = body;
            Parameters = parameters;
        }
    }
}