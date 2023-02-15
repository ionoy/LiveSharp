using LiveSharp.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections;

namespace LiveSharp.CSharp
{
    public class AssemblyUpdateProcessor
    {
        private readonly AssemblyDefinition _originalAssembly;
        private readonly AssemblyDefinition _updatedAssembly;
        private readonly ILogger _logger;

        public AssemblyUpdateProcessor(AssemblyDefinition originalAssembly, AssemblyDefinition updatedAssembly, ILogger logger)
        {
            _originalAssembly = originalAssembly;
            _updatedAssembly = updatedAssembly;
            _logger = logger;
        }

        // public AssemblyDiff CreateDiff()
        // {
        //     var diff = new AssemblyDiff();
        //     var updatedTypes = _updatedAssembly.GetAllTypes().ToDictionary(td => td.FullName.Replace("/", "+"));
        //     var originalTypes = _originalAssembly != null 
        //         ? GetAllTypes(_originalAssembly).ToDictionary(td => td.FullName.Replace("/", "+"))
        //         : new Dictionary<string, TypeDefinition>();
        //
        //     foreach (var kvp in updatedTypes) {
        //         var name = kvp.Key;
        //         var updateType = kvp.Value;
        //         
        //         if (updateType.IsInterface)
        //             continue;
        //
        //         originalTypes.TryGetValue(name, out var originalType);
        //         
        //         ProcessMembers(updateType, originalType, diff);
        //     }
        //
        //     return diff;
        // }
        //
        // private void ProcessMembers(TypeDefinition updatedType, TypeDefinition originalType, AssemblyDiff diff)
        // {
        //     foreach (var newField in updatedType.Fields) {
        //         if (originalType == null || !originalType.Fields.Any(sameField))
        //             diff.NewFields.Add(newField);
        //
        //         bool sameField(FieldDefinition left) {
        //             return left.Name == newField.Name && left.FieldType.FullName == newField.FieldType.FullName;
        //         }
        //     }
        //
        //     foreach (var newProperty in updatedType.Properties) {
        //         if (originalType == null || !originalType.Properties.Any(sameProperty))
        //             diff.NewProperties.Add(newProperty);
        //
        //         bool sameProperty(PropertyDefinition oldProp) {
        //             return oldProp.Name == newProperty.Name && oldProp.PropertyType.FullName == newProperty.PropertyType.FullName;
        //         }
        //     }
        //     
        //     foreach (var newEvent in updatedType.Events) {
        //         if (originalType == null || !originalType.Events.Any(sameEvent))
        //             diff.NewEvents.Add(newEvent);
        //
        //         bool sameEvent(EventDefinition oldEvent) {
        //             return oldEvent.Name == newEvent.Name && oldEvent.EventType.FullName == newEvent.EventType.FullName;
        //         }
        //     }
        //
        //     foreach (var newMethod in updatedType.Methods) {
        //         // Should we do something with empty methods?
        //         if (!newMethod.HasBody)
        //             continue;
        //
        //         // if (newMethod.HasGenericParameters)
        //         //     continue;
        //         
        //         var sameMethodFound = originalType != null && originalType.Methods.Any(oldMethod => oldMethod.SameAs(newMethod));
        //
        //         if (!sameMethodFound)
        //             diff.NewMethods.Add(newMethod);
        //     }
        // }

        private static IEnumerable<TypeDefinition> GetAllTypes(AssemblyDefinition assembly)
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