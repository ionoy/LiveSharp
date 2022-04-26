using System;
using System.Collections.Generic;
using System.Linq;
using LiveSharp.Ide.Serialization;
using LiveSharp.Infrastructure;
using LiveSharp.Runtime;
using LiveSharp.Runtime.Virtual;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LiveSharp.ProjectTester
{
    class Program
    {
        static void Main(string[] args)
        {
            // You need toL
            // 1) add to csproj: <Import Project="c:\Projects\LiveSharp\build\LiveSharp.targets" />
            // 2) build project externally
            // 3) now you can run this utility with csproj path as argument
            //var csprojFile = args[0];
            var (workspace, solutionCacheDir) = WorkspaceLoader.GetWorkspace(@"c:\Projects\LiveSharp\build\", @"c:\Projects\SmartHotel360-Mobile\Source\SmartHotel.Clients.sln").Result;
            var project = workspace.CurrentSolution.Projects.FirstOrDefault(p => p.Name == "SmartHotel.Clients");
            var methodCount = 0;
            
            foreach (var document in project.Documents) {
                Console.WriteLine("Processing document " + document.Name);
                
                var root = document.GetSyntaxRootAsync().Result;  
                var semanticModel = document.GetSemanticModelAsync().Result;
                
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                var structs = root.DescendantNodes().OfType<StructDeclarationSyntax>();
                
                processType(classes, semanticModel);
                processType(structs, semanticModel);
            }

            void processType(IEnumerable<SyntaxNode> types, SemanticModel semanticModel)
            {
                foreach (var type in types) {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(type) as INamedTypeSymbol;
                    if (typeSymbol == null || typeSymbol.IsGenericType)
                        continue;
                    
                    var methods = type.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();

                    foreach (var method in methods.Where(m => m.Body != null || m.ExpressionBody != null)) {
                        Console.WriteLine($"{methodCount++} Method {method.Identifier}");
                        var methodSymbol = (IMethodSymbol) semanticModel.GetDeclaredSymbol(method);
                        if (methodSymbol.IsGenericMethod) {
                            Console.WriteLine("  generic, skipping");
                            continue;
                        }

                        var builder = new ExpressionTreeBuilder(method, semanticModel);
                        var et = builder.GetSerializedMethodBody();
                        //var deserializer = new ExpressionDeserializer(et, null, new VirtualAssembly());
                        //var expression = deserializer.GetExpression();
                    }
                }
            }
        }
    }
}