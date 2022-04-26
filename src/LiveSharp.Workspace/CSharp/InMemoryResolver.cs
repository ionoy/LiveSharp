using Mono.Cecil;

namespace LiveSharp.CSharp
{
    class InMemoryResolver : DefaultAssemblyResolver
    {
        public override AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            parameters.InMemory = true;
            return base.Resolve(name, parameters);
        }
    }
}