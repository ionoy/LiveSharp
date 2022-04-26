using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LiveSharp.Rewriters
{
    public class UpdateHookRewriter : RewriterBase
    {
        private readonly RewriteLogger _logger;
        private readonly ModuleDefinition _mainModule;
        private readonly RuntimeMembers _runtimeMembers;
        private readonly DelegateCacheRewriter _delegateCacheRewriter;
        
        public List<MethodDefinition> MethodsToInject { get; } = new();
        public List<MethodDefinition> InjectedMethods { get; } = new();

        public List<InjectRule> InjectRules { get; } = new();
        public List<InjectRule> InjectExcludeRules { get; } = new();

        public UpdateHookRewriter(RewriteLogger logger, ModuleDefinition mainModule, RuntimeMembers runtimeMembers)
        {
            _logger = logger;
            _mainModule = mainModule;
            _runtimeMembers = runtimeMembers;
            _delegateCacheRewriter = new DelegateCacheRewriter();
        }

        public override void ProcessSupportModule(ModuleDefinition module)
        {
        }

        public override void Rewrite()
        {
            _delegateCacheRewriter.CreateDelegateCacheType(_mainModule);
                    
            foreach (var method in MethodsToInject) {
                if (InjectHook(method, _mainModule, _runtimeMembers))
                    InjectedMethods.Add(method);
            }

            _delegateCacheRewriter.BuildDelegateCacheInitializer(_mainModule, _runtimeMembers);
        }

        public override void ProcessMainModuleType(TypeDefinition type)
        {
            var includeTypeRules = MatchesAnyRule(type, InjectRules).ToArray();
            var excludeTypeRules = MatchesAnyRule(type, InjectExcludeRules).ToArray();
                
            foreach (var method in type.Methods) {
                var includeMethodRules = MatchesAnyRule(method, InjectRules).ToArray();
                if (includeTypeRules.Intersect(includeMethodRules).Any()) {
                    var excludeMethodRules = MatchesAnyRule(method, InjectExcludeRules);
                    var isExcluded = excludeTypeRules.Intersect(excludeMethodRules).Any();
                        
                    if (!isExcluded)
                        MethodsToInject.Add(method);
                }
            }
        }

        private bool InjectHook(MethodDefinition method, ModuleDefinition module, RuntimeMembers runtimeMembers)
        {
            try {
                var body = method.Body;

                if (body == null)
                    return false;
                
                var delegateInfo = _delegateCacheRewriter.GetDelegateInfo(method, module, runtimeMembers);
                var firstInstruction = body.Instructions.First();
                var il = body.GetILProcessor();
                var ret = body.Instructions.Last();

                var delegateField = delegateInfo.DelegateField;
                var delegateType = delegateField.FieldType;
                var delegateInvokeMethod = delegateInfo.DelegateInvokeMethod;
                
                if (delegateInfo.IsGeneric) {
                    var fieldDeclaringType = delegateField.DeclaringType;
                    var allGenericParameters = method.CollectGenericParameters();
                    
                    // Create field reference with proper generic arguments
                    var containerGenericInstance = new GenericInstanceType(fieldDeclaringType);
                    foreach (var genericParameter in allGenericParameters) 
                        containerGenericInstance.GenericArguments.Add(genericParameter);
                    
                    // Supply current generic parameters to the delegate type
                    var genericDelegateType = new GenericInstanceType(delegateType.GetElementType());
                    foreach (var genericParameter in allGenericParameters) 
                        genericDelegateType.GenericArguments.Add(genericParameter);
                    
                    delegateField = new FieldReference(delegateField.Name, delegateType, containerGenericInstance);
                    
                    // Call VirtualClr.TryUpdateGenericDelegateMethod
                    var constructedDelegateVersionField = new FieldReference("__constructedDelegateVersion", module.TypeSystem.Int32, containerGenericInstance);
                    var tryUpdateGenericDelegateMethod = new GenericInstanceMethod(runtimeMembers.TryUpdateGenericDelegateMethod);
                    
                    tryUpdateGenericDelegateMethod.GenericArguments.Add(genericDelegateType);
                    
                    var newDelegateInvokeMethod = new MethodReference("Invoke", delegateInvokeMethod.ReturnType, genericDelegateType) {
                        CallingConvention = delegateInvokeMethod.CallingConvention,
                        ExplicitThis = delegateInvokeMethod.ExplicitThis,
                        HasThis = delegateInvokeMethod.HasThis
                    };
                    
                    foreach (var parameter in delegateInvokeMethod.Parameters)
                        newDelegateInvokeMethod.Parameters.Add (new ParameterDefinition (parameter.ParameterType));

                    foreach (var genericParameter in delegateInvokeMethod.GenericParameters)
                        newDelegateInvokeMethod.GenericParameters.Add (new GenericParameter (genericParameter.Name, newDelegateInvokeMethod));

                    delegateInvokeMethod = newDelegateInvokeMethod;
                    
                    // Check if version != 0
                    InsertBefore(il, firstInstruction,
                        il.Create(OpCodes.Ldsfld, delegateInfo.VersionField),
                        il.Create(OpCodes.Brfalse, firstInstruction)); // load `this`
                    
                    InsertBefore(il, firstInstruction, il.Create(OpCodes.Ldsfld, delegateInfo.VersionField));
                    InsertBefore(il, firstInstruction, il.Create(OpCodes.Ldsflda, constructedDelegateVersionField));
                    InsertBefore(il, firstInstruction, il.Create(OpCodes.Ldsfld, delegateInfo.MethodInfoField));
                    InsertBefore(il, firstInstruction, il.Create(OpCodes.Ldsflda, delegateField));
                    InsertBefore(il, firstInstruction, il.Create(OpCodes.Call, tryUpdateGenericDelegateMethod));
                } else {
                    InsertBefore(il, firstInstruction,
                        il.Create(OpCodes.Ldsfld, delegateField),
                        il.Create(OpCodes.Ldnull),
                        il.Create(OpCodes.Cgt_Un),
                        il.Create(OpCodes.Brfalse, firstInstruction)); // load `this`
                }

                InsertBefore(il, firstInstruction, il.Create(OpCodes.Ldsfld, delegateField));

                if (!method.IsStatic) {
                    InsertBefore(il, firstInstruction, il.Create(OpCodes.Ldarg_0));
                }
                
                LoadArguments(il, firstInstruction, method);

                InsertBefore(il, firstInstruction, il.Create(OpCodes.Callvirt, delegateInvokeMethod));
                
                InsertBefore(il, firstInstruction, 
                    il.Create(OpCodes.Ldsfld, delegateInfo.VersionField),
                    il.Create(OpCodes.Brtrue, ret));

                if (delegateInvokeMethod.ReturnType.GetTypeNoModifier() != module.TypeSystem.Void)
                    InsertBefore(il, firstInstruction, il.Create(OpCodes.Pop));

                InsertBefore(il, firstInstruction, il.Create(OpCodes.Br, firstInstruction));
            } catch (Exception e) {
                _logger.LogWarning($"Injecting code into {method.Name} failed: " + e.Message);
            }

            return true;
        }
        private static void LoadArguments(ILProcessor il, Instruction insertBefore, MethodDefinition method)
        {
            if (method.Parameters.Count == 0)
                return;

            var thisOffset = method.IsStatic ? 0 : 1;

            for (int i = 0; i < method.Parameters.Count; i++)
                il.InsertBefore(insertBefore, il.Create(OpCodes.Ldarg, i + thisOffset));
        }

        private static void InsertBefore(ILProcessor il, Instruction instruction, params Instruction[] instructions)
        {
            var current = instruction;
            for (int i = instructions.Length - 1; i >= 0; i--) {
                il.InsertBefore(current, instructions[i]);
                current = instructions[i];
            }
        }
        
        private IEnumerable<InjectRule> MatchesAnyRule(TypeDefinition typeToCheck, List<InjectRule> rules)
        {
            if (typeToCheck.IsValueType)
                yield break;

            foreach (var rule in rules)
                if (matchType(rule, typeToCheck))
                    yield return rule;

            bool matchType(InjectRule rule, TypeDefinition type)
            {
                while (type != null) {
                    foreach (var iface in type.Interfaces)
                        if (rule.MatchesType(iface.InterfaceType.FullName))
                            return true;

                    if (rule.MatchesType(type.FullName))
                        return true;

                    if (rule.NeedBaseTypeCheck) {
                        type = type.BaseType?.Resolve();
                    }
                    else
                        break;
                }

                return false;
            }
        }

        private IEnumerable<InjectRule> MatchesAnyRule(MethodDefinition md, List<InjectRule> rules)
        {
            // if (md.HasAttribute("System.Runtime.CompilerServices.CompilerGeneratedAttribute"))
            //     return false;

            // if (md.HasGenericParameters)
            //     yield break;
            
            //var isPropertyMethod = md.Name.StartsWith("get_") || md.Name.StartsWith("set_");
            var parameterTypeNames = md.Parameters.Select(p => p.ParameterType.FullName).ToArray();
            
            foreach (var rule in rules) {
                if (rule.MatchesMethod(md.Name)) {
                    if (rule.MatchesParameters(parameterTypeNames))
                        yield return rule;
                }
            }
        }
    }
}