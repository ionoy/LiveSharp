using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

namespace LiveSharp.Rewriters
{
    public class RuntimeMembers
    {
        private readonly ModuleDefinition _mainModule;
        private readonly RewriteLogger _logger;
        public MethodReference RuntimeStartMethod { get; private set; }
        public MethodReference RuntimeAddHandlerMethod { get; private set; }
        public MethodReference RuntimeAddSettingMethod { get; private set; }
        public MethodReference RuntimeAddDelegateFieldMapping { get; private set; }

        public TypeReference MulticastDelegateType { get; private set; }
        public TypeReference IAsyncResultType { get; private set; }
        public TypeReference AsyncCallbackType { get; private set; }
        

        public TypeReference TypeInfoType { get; private set; }
        public MethodReference GetTypeInfoMethod { get; private set; }
        public MethodBody GetTypeInfoMethodBody { get; private set; }
        public InterfaceImplementation IReflectableType { get; private set; }

        public MethodReference GetTypeFromHandleMethod { get; private set; }
        public MethodReference TryUpdateGenericDelegateMethod { get; private set; }

        private RuntimeMembers(ModuleDefinition liveSharpRuntimeModule, ModuleDefinition mainModule, RewriteLogger logger)
        {
            if (liveSharpRuntimeModule == null) throw new ArgumentNullException(nameof(liveSharpRuntimeModule));
            if (mainModule == null) throw new ArgumentNullException(nameof(mainModule));
            
            _mainModule = mainModule;
            _logger = logger;

            ProcessRuntimeAssembly(liveSharpRuntimeModule, mainModule);
        }

        public void TryCollectMembers(TypeDefinition type)
        {
            if (type.FullName == "LiveSharp.Runtime.LiveSharpRuntime") {
                foreach (var method in type.Methods) {
                    if (method.Name == "Start") RuntimeStartMethod = method;
                    else if (method.Name == "AddHandler") RuntimeAddHandlerMethod = method;
                    else if (method.Name == "AddSetting") RuntimeAddSettingMethod = method;
                    else if (method.Name == "AddDelegateFieldMapping") RuntimeAddDelegateFieldMapping = method;
                }

                var multicastDelegateField = type.Fields.FirstOrDefault(f => f.Name == "MulticastDelegateForBuildTaskDontRemove");
                if (multicastDelegateField == null)
                    throw new MissingFieldException("MulticastDelegateForBuildTaskDontRemove");

                var asyncCallbackField = type.Fields.FirstOrDefault(f => f.Name == "AsyncCallbackForBuildTaskDontRemove");
                if (asyncCallbackField == null)
                    throw new MissingFieldException("AsyncCallbackForBuildTaskDontRemove");

                var asyncResultField = type.Fields.FirstOrDefault(f => f.Name == "IAsyncResultForBuildTaskDontRemove");
                if (asyncResultField == null)
                    throw new MissingFieldException("IAsyncResultForBuildTaskDontRemove");
                        
                MulticastDelegateType = multicastDelegateField.FieldType;
                AsyncCallbackType = asyncCallbackField.FieldType;
                IAsyncResultType = asyncResultField.FieldType;
            } 
            else if (type.FullName == "LiveSharp.Runtime.Virtual.VirtualTypeBase")
            {   
                var getTypeInfoMethod = type.Methods.FirstOrDefault(m => m.Name == "GetTypeInfo");

                if (getTypeInfoMethod == null)
                    throw new Exception("Couldn't find GetTypeInfo method on VirtualTypeBase");

                GetTypeInfoMethod = _mainModule.ImportReference(getTypeInfoMethod);
                GetTypeInfoMethodBody = getTypeInfoMethod.Body;
                IReflectableType = type.Interfaces.FirstOrDefault(i => i.InterfaceType.FullName == "System.Reflection.IReflectableType");
                TypeInfoType = _mainModule.ImportReference(GetTypeInfoMethod.ReturnType);
                
                var getTypeInfoMethodBody = GetTypeInfoMethodBody;

                foreach (var item in getTypeInfoMethodBody.Instructions)
                {
                    if (item.Operand is MethodReference method && method.Module != _mainModule)
                        item.Operand = _mainModule.ImportReference(method);
                }
                
                if (IReflectableType == null) 
                    throw new Exception("Couldn't find IReflectableTypeType interface on VirtualTypeBase");
            } else if (type.FullName == "LiveSharp.Runtime.Virtual.VirtualClr") {
                var tryUpdateGenericDelegateMethod = type.Methods.FirstOrDefault(m => m.Name == "TryUpdateGenericDelegate");

                TryUpdateGenericDelegateMethod = _mainModule.ImportReference(tryUpdateGenericDelegateMethod);
            }
        }

        public void ProcessRuntimeAssembly(ModuleDefinition liveSharpRuntimeModule, ModuleDefinition mainModule)
        {
            foreach (var type in liveSharpRuntimeModule.Types)
                TryCollectMembers(type);
            
            ImportRuntimeMethodsInto(mainModule);

            GetTypeFromHandleMethod = FindGetTypeFromHandleMethod(mainModule);
        }

        private MethodReference FindGetTypeFromHandleMethod(ModuleDefinition module)
        {
            try {
                var getType = module.TypeSystem.Object.Resolve().Methods.FirstOrDefault(m => m.Name == "GetType");
                var typeType = getType.ReturnType;
                var typeTypeDefinition = typeType.Resolve();
                var typeTypeMethods = typeTypeDefinition.Methods;
                var getTypeFromHandleMethod = typeTypeMethods.FirstOrDefault(m => m.Name == "GetTypeFromHandle");
            
                return module.ImportReference(getTypeFromHandleMethod);
            }
            catch (Exception e) {
                _logger.LogWarning("Unable to find GetTypeFromHandle method" + Environment.NewLine + e);
                return null;
            }
        }

        private void ImportRuntimeMethodsInto(ModuleDefinition mainModule)
        {
            RuntimeStartMethod = mainModule.ImportReference(RuntimeStartMethod);
            RuntimeAddHandlerMethod = mainModule.ImportReference(RuntimeAddHandlerMethod);
            RuntimeAddSettingMethod = mainModule.ImportReference(RuntimeAddSettingMethod);
            RuntimeAddDelegateFieldMapping = mainModule.ImportReference(RuntimeAddDelegateFieldMapping);
                    
            MulticastDelegateType = mainModule.ImportReference(MulticastDelegateType);
            AsyncCallbackType = mainModule.ImportReference(AsyncCallbackType);
            IAsyncResultType = mainModule.ImportReference(IAsyncResultType);
        }

        public static RuntimeMembers FromRuntimeAssembly(ModuleDefinition liveSharpRuntimeModule, ModuleDefinition mainModule, RewriteLogger logger)
        {
            var runtimeMembers = new RuntimeMembers(liveSharpRuntimeModule, mainModule, logger);
            var isRuntimeSet = runtimeMembers.RuntimeStartMethod != null &&
                               runtimeMembers.RuntimeAddHandlerMethod != null && 
                               runtimeMembers.RuntimeAddSettingMethod != null &&
                               runtimeMembers.RuntimeAddDelegateFieldMapping != null;
            
            if (!isRuntimeSet)
                throw new Exception("LiveSharp task couldn't find LiveSharpRuntime members");
            
            return runtimeMembers;
        }
    }
}