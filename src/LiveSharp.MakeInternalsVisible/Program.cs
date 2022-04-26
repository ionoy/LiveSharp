using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace LiveSharp.MakeInternalsVisible
{
    class Program
    {
        private static string _dashboardAssemblyName = "LiveBlazor.Dashboard, PublicKey=0024000004800000140100000602000000240000525341310008000001000100b72da9a756f5789d8573eda75ed086b1257ff762852ed92cf3716c2a93fd52f4a83bc3186ce57cdd484c3dedd304442c10773bb21445766b3301b53c5bbe9d157fed1ff1fb0d4c7e2a8ff6e0c8ad43b524f42fece8cf669808f6471ae0d962ba6fc752b990b8c172cf7df45b81ce2377ecea50b5fe3f48787475a30bd364fd7b350c2230f37880e503a82f960bfad2f7f92032d128b1ff1d151519f0c66ad93b006dd2e43add8a0adfa82346c150802a4ccca45f0af0785418b2160153907313f346ea9a22dafeed41789442263b49890e33dc7a7a5c8b4f0772e587a6b40b202202a4cab6bbba8e520fddf0d6d74d4f4a6da8916f682b45acda4cb7778297b1";
        static void Main(string[] args)
        {
            if (args.Length == 0) {
                Console.WriteLine("Assembly path missing");
                return;
            }
            
            var mainAssemblyName = args[0];
            var assemblyDirectory = Path.GetDirectoryName(mainAssemblyName);
            var fileInDirectory = Directory.GetFiles(assemblyDirectory);
            var assemblyNames = new List<string>();

            assemblyNames.Add(_dashboardAssemblyName);
            
            RewriteInternalsVisibleTo(mainAssemblyName, assemblyNames);
        }

        static void RewriteInternalsVisibleTo(string mainAssemblyName, List<string> assemblyNames)
        {
            var readerParameters = new ReaderParameters {
                ReadWrite = true
            };
            
            using var assembly = AssemblyDefinition.ReadAssembly(mainAssemblyName, readerParameters);
            
            var mainModule = assembly.MainModule;
            var internalsVisibleToType = FindType(assembly, "System.Runtime.CompilerServices.InternalsVisibleToAttribute");
            
            internalsVisibleToType = mainModule.ImportReference(internalsVisibleToType);
            
            var internalsVisibleToAttributes = assembly.CustomAttributes.Where(a => a.AttributeType.Name == "InternalsVisibleToAttribute").ToArray();
            var internalsVisibleToCtor = mainModule.ImportReference(internalsVisibleToType.Resolve().GetConstructors().FirstOrDefault());

            if (internalsVisibleToAttributes.Any(a => a.ConstructorArguments.Any(arg => arg.Value?.ToString() == _dashboardAssemblyName)))
                return;
            // foreach (var internalsVisibleToAttribute in internalsVisibleToAttributes)
            //     assembly.CustomAttributes.Remove(internalsVisibleToAttribute);

            foreach (var assemblyName in assemblyNames) {
                var stringType = assembly.MainModule.TypeSystem.String;
                var attributeArgument = new CustomAttributeArgument(stringType, assemblyName);
                var attribute = new CustomAttribute(internalsVisibleToCtor) {
                    ConstructorArguments = {
                        attributeArgument
                    }
                };
                
                assembly.CustomAttributes.Add(attribute);
            }

            assembly.Write(Path.ChangeExtension(mainAssemblyName, ".dll"));
        }

        static TypeReference FindType(AssemblyDefinition assembly, string typeName)
        {
            //Debug.WriteLine($"Looking in {assembly.FullName}");
            
            foreach (var type in assembly.MainModule.Types) {
                if (type.FullName == typeName)
                    return type;
            }

            foreach (var assemblyReference in assembly.MainModule.AssemblyReferences) {
                var resolvedReference = assembly.MainModule.AssemblyResolver.Resolve(assemblyReference);
                
                if (FindType(resolvedReference, typeName) is { } tr)
                    return tr;
            }

            return null;
        }
    }
}