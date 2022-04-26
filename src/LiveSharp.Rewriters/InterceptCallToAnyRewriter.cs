using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LiveSharp.Rewriters
{
    public class InterceptCallToAnyRewriter : IIlRewriter
    {
        private readonly string _typeName;
        private readonly Dictionary<MethodDefinition, List<VariableDefinition>> _argumentLocals = new();

        public InterceptCallToAnyRewriter(CustomAttribute attribute, MethodDefinition interceptorMethod)
        {
            InterceptorMethod = interceptorMethod;
            
            var arguments = attribute.ConstructorArguments;
            
            if (arguments[0].Type.FullName == typeof(TypeReference).FullName)
                _typeName = ((TypeReference)arguments[0].Value).FullName;
            if (arguments[0].Type.FullName == typeof(Type).FullName)
                _typeName = ((TypeReference)arguments[0].Value).FullName;
            else if (arguments[0].Type.FullName == typeof(String).FullName)
                _typeName = ((string)arguments[0].Value);
        }

        public MethodReference InterceptorMethod { get; set; }
        public bool Matches(Instruction instruction)
        {
            if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) {
                if (instruction.Operand is MethodReference mr && mr.DeclaringType.Is(_typeName)) {
                    var md = mr.Resolve();
                    // treat extension methods as instance for this case
                    var isInstanceMethod = !md.IsStatic || md.IsExtensionMethod();
                    
                    if (isInstanceMethod && InterceptorMethod.Parameters.Count == 2)
                        return true;
                    
                    if (!isInstanceMethod && InterceptorMethod.Parameters.Count == 1)
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

            var methodDefinition = methodReference.Resolve();
            var isStatic = methodDefinition.IsStatic;
            
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

            // load instance for instance methods and extension methods
            if (!isStatic || methodDefinition.IsExtensionMethod())
                ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Ldloc, localList[0]));
            
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Ldstr, methodDefinition.GetMethodIdentifier()));
            
            // Call to the interceptor
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Call, InterceptorMethod));
            
            // Load arguments again for the original call
            foreach (var argumentLocal in localList) 
                ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Ldloc, argumentLocal));

            return instruction;
        }
    }
}