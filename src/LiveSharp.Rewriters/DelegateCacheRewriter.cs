using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LiveSharp.Rewriters
{
    public class DelegateCacheRewriter
    {
        private readonly Dictionary<MethodDefinition, FieldDefinition> _staticDelegateMapping = new();
        private readonly Dictionary<MethodDefinition, FieldDefinition> _genericDelegateMapping = new();
        private readonly Dictionary<TypeReference, TypeDefinition> _delegateCacheTypes = new();
        private static FieldDefinition _delegateCacheInitializerField;
        private static TypeDefinition _delegateCacheType;

        public void BuildDelegateCacheInitializer(ModuleDefinition module, RuntimeMembers runtimeMembers)
        {
            var moduleInitializerMethod = module.GetOrCreateModuleInitializerMethod();
            var il = moduleInitializerMethod.Body.GetILProcessor();

            if (il.Body.Instructions.LastOrDefault()?.OpCode == OpCodes.Ret) {
                il.RemoveAt(il.Body.Instructions.Count - 1);
            }

            var typeType = module.ImportReference(module.TypeSystem.Object.Resolve().Methods.FirstOrDefault(m => m.Name == "GetType")?.ReturnType);
            
            foreach (var delegateFieldMappingPair in _staticDelegateMapping.Concat(_genericDelegateMapping)) {
                var methodDefinition = delegateFieldMappingPair.Key;
                var fieldDefinition = delegateFieldMappingPair.Value;
                
                il.Append(Instruction.Create(OpCodes.Ldtoken, methodDefinition.DeclaringType));
                il.Append(Instruction.Create(OpCodes.Call, runtimeMembers.GetTypeFromHandleMethod));
                il.Append(Instruction.Create(OpCodes.Ldstr, methodDefinition.Name));
                il.Append(Instruction.Create(OpCodes.Ldstr, methodDefinition.GetMethodIdentifier()));
                il.Append(Instruction.Create(OpCodes.Ldtoken, fieldDefinition.DeclaringType));
                il.Append(Instruction.Create(OpCodes.Call, runtimeMembers.GetTypeFromHandleMethod));
                il.Append(Instruction.Create(OpCodes.Ldstr, fieldDefinition.Name));
                
                var returnType = methodDefinition.ReturnType;
                if (returnType is GenericParameter || returnType.HasGenericParameters || returnType.ContainsGenericParameter) {
                    returnType = module.TypeSystem.Object;
                }
                
                il.Append(Instruction.Create(OpCodes.Ldtoken, returnType));
                il.Append(Instruction.Create(OpCodes.Call, runtimeMembers.GetTypeFromHandleMethod));
                
                il.Append(Instruction.Create(OpCodes.Ldc_I4, methodDefinition.Parameters.Count));
                il.Append(Instruction.Create(OpCodes.Newarr, typeType));
                
                for (var i = 0; i < methodDefinition.Parameters.Count; i++) {
                    var parameter = methodDefinition.Parameters[i];
                    var parameterType = parameter.ParameterType;

                    if (parameterType is GenericParameter || parameterType.HasGenericParameters || parameterType.ContainsGenericParameter) {
                        parameterType = module.TypeSystem.Object;
                    }
                    
                    il.Append(Instruction.Create(OpCodes.Dup));
                    il.Append(Instruction.Create(OpCodes.Ldc_I4, i));
                    il.Append(Instruction.Create(OpCodes.Ldtoken, parameterType));
                    il.Append(Instruction.Create(OpCodes.Call, runtimeMembers.GetTypeFromHandleMethod));
                    il.Append(Instruction.Create(OpCodes.Stelem_Ref));
                }
                
                il.Append(Instruction.Create(OpCodes.Call, runtimeMembers.RuntimeAddDelegateFieldMapping));
            }
            
            il.Append(Instruction.Create(OpCodes.Ret));
        }

        public DelegateInfo GetDelegateInfo(MethodDefinition method, ModuleDefinition module, RuntimeMembers runtimeMembers)
        {
            if (!_delegateCacheTypes.TryGetValue(method.DeclaringType, out var nestedDelegateCacheType)) {
                var nestedDelegateCacheTypeName = "<>" + method.DeclaringType.MetadataToken.ToInt32() + method.DeclaringType.Name;
                nestedDelegateCacheType = new TypeDefinition("LiveSharp", nestedDelegateCacheTypeName, TypeAttributes.Class | TypeAttributes.NestedPublic, module.TypeSystem.Object);
                
                _delegateCacheTypes[method.DeclaringType] = nestedDelegateCacheType;
                _delegateCacheType.NestedTypes.Add(nestedDelegateCacheType);
            }
            
            var delegateTypeName = "<>" + method.MetadataToken.ToInt32() + "_" + method.Name;

            if (method.IsStatic) 
                delegateTypeName += "$s";

            var allGenericParameters = method.CollectGenericParameters();
            var methodDelegateType = CreateDelegateTypeFromMethod(delegateTypeName, module, method, runtimeMembers, allGenericParameters);
            var hasMethodUpdateFieldName = delegateTypeName + "_version";
            var hasMethodUpdateField = new FieldDefinition(hasMethodUpdateFieldName, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.RTSpecialName, module.TypeSystem.Int32);
            
            nestedDelegateCacheType.Fields.Add(hasMethodUpdateField);
            nestedDelegateCacheType.NestedTypes.Add(methodDelegateType);
            
            FieldDefinition delegateField;
            FieldDefinition methodInfoField = null;
            
            if (allGenericParameters.Count > 0) {
                var delegateContainer = CreateGenericDelegateContainer(delegateTypeName, module, method);
                
                // Create delegate field inside the container
                var constructedDelegateType = new GenericInstanceType(methodDelegateType);
                foreach (var containerGenericParameter in delegateContainer.GenericParameters)
                    constructedDelegateType.GenericArguments.Add(containerGenericParameter);

                delegateField = new FieldDefinition("__delegate", FieldAttributes.Static | FieldAttributes.Public, constructedDelegateType);
                delegateContainer.Fields.Add(delegateField);

                // Add container to the parent type
                nestedDelegateCacheType.NestedTypes.Add(delegateContainer);

                // Create a field to store VirtualMethodInfo updates
                methodInfoField = new FieldDefinition(delegateTypeName + "_methodInfo", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Object);
                nestedDelegateCacheType.Fields.Add(methodInfoField);
                _genericDelegateMapping[method] = methodInfoField;
            } else {
                delegateField = new FieldDefinition(delegateTypeName + "_u", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.RTSpecialName, methodDelegateType);
                
                nestedDelegateCacheType.Fields.Add(delegateField);
                _staticDelegateMapping[method] = delegateField;
            }
             
                
            var delegateInvokeMethod = methodDelegateType.Methods.FirstOrDefault(m => m.Name == "Invoke");
            if (delegateInvokeMethod == null)
                throw new Exception("Invoke method not found on " +  methodDelegateType);
                
            return new DelegateInfo(delegateField, delegateInvokeMethod, hasMethodUpdateField, allGenericParameters, methodInfoField, nestedDelegateCacheType);
        }

        private TypeDefinition CreateGenericDelegateContainer(string delegateTypeName, ModuleDefinition module, MethodDefinition method)
        {
            var containerType = new TypeDefinition(null, delegateTypeName + "_constructedDelegateContainer", TypeAttributes.Class | TypeAttributes.NestedPublic, module.TypeSystem.Object);
            
            foreach (var methodGenericParameter in method.CollectGenericParameters())
                containerType.GenericParameters.Add(new GenericParameter(methodGenericParameter.Name + "C", containerType));

            var versionField = new FieldDefinition("__constructedDelegateVersion", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Int32);
            containerType.Fields.Add(versionField);
            
            return containerType;
        }

        public void CreateDelegateCacheType(ModuleDefinition module)
        {
            _delegateCacheType = new TypeDefinition(null, "<liveSharpDelegateCache>", TypeAttributes.Class, module.TypeSystem.Object);
            _delegateCacheInitializerField = new FieldDefinition("__initializer", FieldAttributes.Static | FieldAttributes.Public, module.TypeSystem.Boolean);
            
            _delegateCacheType.Fields.Add(_delegateCacheInitializerField);
            
            module.Types.Add(_delegateCacheType);
        }
        
        private TypeDefinition CreateDelegateTypeFromMethod(string delegateTypeName, ModuleDefinition module, MethodDefinition method, RuntimeMembers runtimeMembers, List<GenericParameter> allGenericParameters)
        {
            // if (delegateTypeName.Contains("g__getAssertCallCount")) {
            //     Debugger.Launch();
            // }
            var delegateType = new TypeDefinition(null, delegateTypeName, TypeAttributes.Sealed | TypeAttributes.NestedAssembly | TypeAttributes.RTSpecialName, runtimeMembers.MulticastDelegateType);
            
            var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.CompilerControlled | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.HideBySig, module.TypeSystem.Void);
            var beginInvoke = new MethodDefinition("BeginInvoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual, runtimeMembers.IAsyncResultType);
            var endInvoke = new MethodDefinition("EndInvoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual, module.TypeSystem.Void);
            var invoke = new MethodDefinition("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual, module.ImportReference(method.ReturnType));
            
            ctor.IsRuntime = endInvoke.IsRuntime = beginInvoke.IsRuntime = invoke.IsRuntime = true;

            // need 'this' for non static methods
            if (!method.IsStatic) {
                beginInvoke.Parameters.Add(new ParameterDefinition("instance", ParameterAttributes.None, module.TypeSystem.Object));
                invoke.Parameters.Add(new ParameterDefinition("instance", ParameterAttributes.None, module.TypeSystem.Object));
            }
            
            foreach (var gp in allGenericParameters)
                delegateType.GenericParameters.Add(new GenericParameter(gp.Name + "D", delegateType));

            invoke.ReturnType = resolveGenericType(invoke.ReturnType);
            
            foreach(var para in method.Parameters) {
                var parameterType = para.ParameterType;

                parameterType = resolveGenericType(parameterType);
                
                beginInvoke.Parameters.Add(new ParameterDefinition(para.Name, ParameterAttributes.None, parameterType));
                invoke.Parameters.Add(new ParameterDefinition(para.Name, ParameterAttributes.None, parameterType));
            }

            ctor.Parameters.Add(new ParameterDefinition("object", ParameterAttributes.None, module.TypeSystem.Object));
            ctor.Parameters.Add(new ParameterDefinition("method", ParameterAttributes.None, module.TypeSystem.IntPtr));
            
            beginInvoke.Parameters.Add(new ParameterDefinition("callback", ParameterAttributes.None, runtimeMembers.AsyncCallbackType));
            beginInvoke.Parameters.Add(new ParameterDefinition("object", ParameterAttributes.None, module.TypeSystem.Object));
            
            endInvoke.Parameters.Add(new ParameterDefinition("result", ParameterAttributes.None, runtimeMembers.IAsyncResultType));
            
            delegateType.Methods.Add(endInvoke);            
            delegateType.Methods.Add(invoke);
            delegateType.Methods.Add(ctor);
            delegateType.Methods.Add(beginInvoke);

            return delegateType;

            TypeReference resolveGenericType(TypeReference type)
            {
                return unwrapElementType(type, sourceType => {
                    // we have all of the generic parameters added to delegate type
                    // find new GP and update parameter type
                    if (sourceType is GenericParameter gp)
                        return delegateType.GenericParameters[allGenericParameters.IndexOf(gp)];

                    if (sourceType is GenericInstanceType git) {
                        sourceType = resolveGenericInstanceType(git);
                    
                        if (sourceType.DeclaringType is GenericInstanceType declaringTypeGit)
                            sourceType.DeclaringType = resolveGenericInstanceType(declaringTypeGit);
                    }

                    return sourceType;
                });
            }
            
            TypeReference unwrapElementType(TypeReference t, Func<TypeReference, TypeReference> action)
            {
                if (t is ArrayType arrayType)
                    return unwrapElementType(arrayType.ElementType, action).MakeArrayType(arrayType.Rank);
                if (t is ByReferenceType brt)
                    return unwrapElementType(brt.ElementType, action).MakeByReferenceType();
                if (t is PointerType pointerType)
                    return unwrapElementType(pointerType.ElementType, action).MakePointerType();
                
                return action(t);
            }
                
            GenericInstanceType resolveGenericInstanceType(GenericInstanceType genericInstanceType)
            {
                var replacedArgs = genericInstanceType
                    .GenericArguments
                    .Select(resolveGenericType);
                var newType = new GenericInstanceType(genericInstanceType.ElementType);
                    
                foreach (var replacedArg in replacedArgs)
                    newType.GenericArguments.Add(replacedArg);
                    
                return newType;
            }
        }
    }

    public class DelegateInfo
    {
        public DelegateInfo(FieldReference delegateField, MethodReference delegateInvokeMethod, FieldReference versionField,
            List<GenericParameter> allGenericParameters, FieldDefinition methodInfoField, TypeDefinition delegateCacheType)
        {
            DelegateField = delegateField;
            DelegateInvokeMethod = delegateInvokeMethod;
            VersionField = versionField;
            AllGenericParameters = allGenericParameters;
            MethodInfoField = methodInfoField;
            DelegateCacheType = delegateCacheType;
        }

        public FieldReference DelegateField { get; }
        public MethodReference DelegateInvokeMethod { get; }
        public FieldReference VersionField { get; }
        public List<GenericParameter> AllGenericParameters { get; }
        public FieldDefinition MethodInfoField { get; }
        public TypeDefinition DelegateCacheType { get; }

        public bool IsGeneric => AllGenericParameters?.Count > 0;
    }
}