using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using LiveSharp;
using System.IO;
using System.Linq;

namespace LiveSharp
{
    public class LiveSharpAssemblyLoadContext : AssemblyLoadContext, ILiveSharpLoadContext
    {
        public Assembly MainAssembly { get; }
        public List<Assembly> ReferenceAssemblies { get; } = new ();
        
        private readonly IEnumerable<LiveSharpAssemblyUpdate> _referenceAssemblies;

        public LiveSharpAssemblyLoadContext(LiveSharpAssemblyUpdate mainAssembly, IEnumerable<LiveSharpAssemblyUpdate> referenceAssemblies)
        {
            _referenceAssemblies = referenceAssemblies;

            MainAssembly = LoadAssemblyUpdate(mainAssembly);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var referenceAssemblyUpdate = _referenceAssemblies.FirstOrDefault(a => a.Name == assemblyName.Name);
            if (referenceAssemblyUpdate != null) {
                var referenceAssembly = LoadAssemblyUpdate(referenceAssemblyUpdate);
                ReferenceAssemblies.Add(referenceAssembly);
                return referenceAssembly;
            }

            return base.Load(assemblyName);
        }

        private Assembly LoadAssemblyUpdate(LiveSharpAssemblyUpdate assemblyUpdate)
        {
            if (assemblyUpdate.SymbolsBuffer != null)
                return LoadFromStream(new MemoryStream(assemblyUpdate.AssemblyBuffer), new MemoryStream(assemblyUpdate.SymbolsBuffer));

            return LoadFromStream(new MemoryStream(assemblyUpdate.AssemblyBuffer));
        }
    }
}