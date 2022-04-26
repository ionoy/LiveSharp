using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace LiveSharp.Rewriters
{
    public abstract class RewriterBase
    {
        public abstract void ProcessSupportModule(ModuleDefinition module);
        
        public void ProcessMainModule(ModuleDefinition module)
        {
            var allTypes = module.GetAllTypes();
            foreach (var type in allTypes) 
                ProcessMainModuleType(type);
        }

        public abstract void ProcessMainModuleType(TypeDefinition type);

        public abstract void Rewrite();
    }
}