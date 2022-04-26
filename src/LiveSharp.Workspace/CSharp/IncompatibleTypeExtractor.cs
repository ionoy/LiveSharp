using LiveSharp.Rewriters;
using Mono.Cecil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LiveSharp.CSharp
{
    // public class IncompatibleTypeExtractor
    // {
    //     private readonly ModuleDefinition _mainModule;
    //     private readonly AssemblyDiff _diff;
    //
    //     private static readonly ConcurrentDictionary<string, int> AssemblyVersions = new ();
    //
    //     public IncompatibleTypeExtractor(ModuleDefinition mainModule, AssemblyDiff diff)
    //     {
    //         _mainModule = mainModule;
    //         _diff = diff;
    //     }
    //     
    //     public bool Extract(out AssemblyDefinition generatedAssembly)
    //     {
    //         generatedAssembly = null;
    //         
    //         var incompatibleTypes = GetIncompatibleTypes(_diff.NewProperties);
    //
    //         if (incompatibleTypes.Count == 0)
    //             return false;
    //         
    //         generatedAssembly = CreateNewAssembly();
    //
    //         while (incompatibleTypes.Count > 0) {
    //             foreach (var incompatibleType in incompatibleTypes) {
    //                 ExtractType(incompatibleType, generatedAssembly);
    //             }
    //         }
    //
    //         return true;
    //     }
    //
    //     private AssemblyDefinition CreateNewAssembly()
    //     {
    //         var newAssemblyRevision = AssemblyVersions.AddOrUpdate(_mainModule.Assembly.Name.Name, _ => 1, (_, version) => version + 1);
    //         var oldVersion = _mainModule.Assembly.Name.Version;
    //         var newVersion = new Version(oldVersion.Major, oldVersion.Minor, oldVersion.Build, newAssemblyRevision);
    //         
    //         var newAssembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(_mainModule.Name, newVersion), _mainModule.Name, _mainModule.Kind);
    //         var newAssemblyMainModule = newAssembly.MainModule;
    //
    //         foreach (var mainModuleAssemblyReference in _mainModule.AssemblyReferences)
    //             newAssemblyMainModule.AssemblyReferences.Add(mainModuleAssemblyReference);
    //
    //         newAssemblyMainModule.AssemblyReferences.Add(_mainModule.Assembly.Name);
    //
    //         return newAssembly;
    //     }
    //
    //     private void ExtractType(TypeDefinition incompatibleType, AssemblyDefinition generatedAssembly)
    //     {
    //         var clonedType = CloneType(incompatibleType, generatedAssembly);
    //
    //     }
    //
    //     // private TypeDefinition CloneType(TypeDefinition type, AssemblyDefinition targetAssembly)
    //     // {
    //     //     var newType = new TypeDefinition(type.Namespace, type.Name, type.Attributes, type.BaseType);
    //     //     newType.
    //     // }
    //
    //     private IReadOnlyList<TypeDefinition> GetIncompatibleTypes(HashSet<PropertyDefinition> newProperties)
    //     {
    //         return newProperties.Where(p => p.DeclaringType.Is("Microsoft.AspNetCore.Components.ComponentBase"))
    //                          .Select(p => p.DeclaringType)
    //                          .Distinct()
    //                          .ToArray();
    //     }
    // }
}