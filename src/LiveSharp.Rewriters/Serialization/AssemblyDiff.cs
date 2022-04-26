using LiveSharp.Rewriters;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace LiveSharp.Rewriters.Serialization
{
    public class AssemblyDiff
    {
        public HashSet<FieldDefinition> NewFields { get; } = new HashSet<FieldDefinition>();
        public HashSet<PropertyDefinition> NewProperties { get; } = new HashSet<PropertyDefinition>();
        public HashSet<EventDefinition> NewEvents { get; } = new HashSet<EventDefinition>();
        public HashSet<MethodDefinition> NewMethods { get; } = new HashSet<MethodDefinition>();

        public bool HasUpdates()
        {
            return NewFields.Any() || NewProperties.Any() || NewEvents.Any() || NewMethods.Any();
        }
    }
}