using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSharp.Rewriters
{
    internal class InterceptCallToRewriter : IIlRewriter
    {
        public MethodReference InterceptorMethod { get; set; }
        
        private readonly string _typeName;
        private readonly string _methodName;
        private readonly bool _needArgumentTransform;

        private readonly Dictionary<MethodDefinition, List<VariableDefinition>> _argumentLocals = new();

        public InterceptCallToRewriter(CustomAttribute attribute, MethodDefinition interceptorMethod)
        {
            InterceptorMethod = interceptorMethod;
            
            var arguments = attribute.ConstructorArguments;
            
            if (arguments[0].Type.FullName == typeof(TypeReference).FullName)
                _typeName = ((TypeReference)arguments[0].Value).FullName;
            if (arguments[0].Type.FullName == typeof(Type).FullName)
                _typeName = ((TypeReference)arguments[0].Value).FullName;
            else if (arguments[0].Type.FullName == typeof(String).FullName)
                _typeName = ((string)arguments[0].Value);
            
            _methodName = arguments[1].Value.ToString();

            if (arguments.Count > 2)
                _needArgumentTransform = (bool)arguments[2].Value;
        }

        public bool Matches(Instruction instruction)
        {
            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) {
                if (instruction.Operand is MethodReference mr && mr.Name == _methodName && mr.DeclaringType.Is(_typeName)) {
                    var methodDefinition = mr.Resolve();
                    var requiredInterceptorParameterCount = mr.Parameters.Count;

                    if (_needArgumentTransform)
                        requiredInterceptorParameterCount *= 2;

                    if (!methodDefinition.IsStatic)
                        requiredInterceptorParameterCount += 1;
                    
                    if (requiredInterceptorParameterCount != InterceptorMethod.Parameters.Count)
                        return false;

                    var interceptorParameterIndex = 0;

                    if (!methodDefinition.IsStatic) 
                        interceptorParameterIndex++;
                    
                    for (var i = 0; i < methodDefinition.Parameters.Count; i++,interceptorParameterIndex++) {
                        var mrParameter = methodDefinition.Parameters[i];
                        var interceptorParameter = InterceptorMethod.Parameters[interceptorParameterIndex];
                        
                        if (!mrParameter.ParameterType.SameReferenceAs(interceptorParameter.ParameterType))
                            return false;
                    }

                    if (_needArgumentTransform) {
                        for (var i = 0; i < methodDefinition.Parameters.Count; i++,interceptorParameterIndex++) {
                            var mrParameter = methodDefinition.Parameters[i];
                            var interceptorParameter = InterceptorMethod.Parameters[interceptorParameterIndex];
                        
                            if (!mrParameter.ParameterType.MakeByReferenceType().SameReferenceAs(interceptorParameter.ParameterType))
                                return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public Instruction Rewrite(MethodDefinition parentMethod, Instruction instruction, ILProcessor ilProcessor)
        {
            var methodReference = instruction.Operand as MethodReference;
            
            if (methodReference == null)
                throw new InvalidOperationException(
                    $"Invalid operand for InterceptCallToRewriter Rewrite {instruction.Operand}");

            var isStatic = methodReference.Resolve().IsStatic;
            
            if (!_argumentLocals.TryGetValue(parentMethod, out var localList)) {
                localList = new List<VariableDefinition>();
                _argumentLocals[parentMethod] = localList;

                if (!isStatic)
                    localList.Add(new VariableDefinition(methodReference.DeclaringType));
                
                foreach (var parameter in methodReference.Parameters)
                    localList.Add(new VariableDefinition(parameter.ParameterType));

                foreach (var local in localList)
                    parentMethod.Body.Variables.Add(local);
            }

            // Store loaded arguments into locals (with instance value because it's accounted by localList)
            foreach (var argumentLocal in Enumerable.Reverse(localList)) {
                ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Stloc, argumentLocal));
            }
            
            // Now load them back on stack
            foreach (var argumentLocal in localList) {
                ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Ldloc, argumentLocal));
            }

            // If we have transforming interceptor (void interceptor(arg0, arg1, out arg0, out arg1))
            // load local refs
            if (_needArgumentTransform) {
                // Skip instance for transform because we don't transform it
                foreach (var argumentLocal in localList.Skip(isStatic ? 0 : 1)) {
                    ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Ldloca, argumentLocal));
                }
            }
            
            // Call to the interceptor
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Call, InterceptorMethod));
            
            // Load arguments again for the original call
            foreach (var argumentLocal in localList) {
                ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Ldloc, argumentLocal));
            }

            return instruction;
        }
    }
}