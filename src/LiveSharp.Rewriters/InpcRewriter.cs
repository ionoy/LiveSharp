using Mono.Cecil;
using System.Collections.Generic;

namespace LiveSharp.Rewriters
{
    public class InpcRewriter : RewriterBase
    {
        private readonly RuntimeMembers _runtimeMembers;
        private readonly HashSet<TypeDefinition> _inpcTypes = new();

        public InpcRewriter(RuntimeMembers runtimeMembers)
        {
            _runtimeMembers = runtimeMembers;
        }

        public override void ProcessSupportModule(ModuleDefinition module)
        {
        }

        public override void ProcessMainModuleType(TypeDefinition type)
        {

            if (type.HasInterface("System.ComponentModel.INotifyPropertyChanged") && !type.HasBaseType("Xamarin.Forms.Element"))
                _inpcTypes.Add(type);
        }

        public override void Rewrite()
        {
            foreach (var inpcType in _inpcTypes) 
                InjectIReflectableTypeInterface(inpcType, _runtimeMembers);
        }
        
        private void InjectIReflectableTypeInterface(TypeReference inpcType, RuntimeMembers runtimeMembers)
        {
            var type = inpcType.Resolve();
            var getTypeInfoMethod = runtimeMembers.GetTypeInfoMethod;
            var newGetTypeInfoMethod = new MethodDefinition(getTypeInfoMethod.Name, runtimeMembers.GetTypeInfoMethod.Resolve().Attributes, getTypeInfoMethod.ReturnType) {
                Body = runtimeMembers.GetTypeInfoMethodBody, 
                ReturnType = runtimeMembers.TypeInfoType, 
                DeclaringType = type
            };

            foreach (var variable in newGetTypeInfoMethod.Body.Variables)
                if (variable.VariableType.FullName == runtimeMembers.TypeInfoType.FullName)
                    variable.VariableType = runtimeMembers.TypeInfoType;
            
            type.Interfaces.Add(runtimeMembers.IReflectableType);
            //newGetTypeInfoMethod.Overrides.Add(getTypeInfoMethod.Overrides.FirstOrDefault());

            type.Methods.Add(newGetTypeInfoMethod);
        }
    }
}