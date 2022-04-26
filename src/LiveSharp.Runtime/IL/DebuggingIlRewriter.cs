using LiveSharp.Runtime.Virtual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace LiveSharp.Runtime.IL
{
    public class DebuggingIlRewriter
    {
        public static readonly MethodInfo StartMethod = typeof(LiveSharpDebugger).GetMethod(nameof(LiveSharpDebugger.Start), new[] { typeof(object), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(object[]) })
                                                        ?? throw new InvalidOperationException("LiveSharpDebugger.Start");

        public static readonly MethodInfo AssignMethodDebug = typeof(LiveSharpDebugger).GetMethod(nameof(LiveSharpDebugger.Assign), new[] { typeof(object), typeof(int), typeof(string)})
                                                              ?? throw new InvalidOperationException("LiveSharpDebugger.Assign");
        
        public static readonly MethodInfo AssignMethod = typeof(LiveSharpDebugger).GetMethod(nameof(LiveSharpDebugger.Assign), new[] { typeof(object), typeof(int), typeof(int)})
                                                         ?? throw new InvalidOperationException("LiveSharpDebugger.Assign");
        
        public static readonly MethodInfo ReturnMethod = typeof(LiveSharpDebugger).GetMethod(nameof(LiveSharpDebugger.Return), new[] { typeof(int), typeof(object)})
                                                         ?? throw new InvalidOperationException("LiveSharpDebugger.Return");
        
        public static readonly MethodInfo RegisterClosureInstanceMethod = typeof(LiveSharpDebugger).GetMethod(nameof(LiveSharpDebugger.RegisterClosureInstance), new[] { typeof(object), typeof(int)})
                                                                          ?? throw new InvalidOperationException("LiveSharpDebugger.RegisterClosureInstance");
        public static IlInstructionList AddDebuggingHandlers(IlInstructionList instructions, VirtualMethodInfo methodInfo)
        {
            var methodBody = methodInfo.GetMethodBody();
            var localNames = string.Join(",", methodBody.Locals.Select(l => l.LocalName));
            var paramNames = string.Join(",", methodInfo.Parameters.Select(p => p.ParameterName));
            var invocationIdLocal = new LocalMetadata("<>invocationId", typeof(int));            
            
            methodBody.Locals.Add(invocationIdLocal);

            var head = instructions.Head;
            
            // if (!IsCompilerGenerated(metadata.TypeMetadata.Type))
            CreateLiveDebuggerStart(instructions, methodInfo, localNames, paramNames, invocationIdLocal);
    
            RewriteMainCode(instructions, methodInfo, methodBody, invocationIdLocal, head);

            return instructions;
        }

        private static void RewriteMainCode(IlInstructionList instructions, VirtualMethodInfo methodInfo, VirtualMethodBody methodBody, LocalMetadata invocationIdLocal, IlInstruction head)
        {
            foreach (var instruction in instructions.SkipWhile(i => i != head)) {
                //Assign debug is after the original instruction `st*` instruction
                if (instruction.OpCode == OpCodes.Stloc || instruction.OpCode == OpCodes.Stloc_S) {
                    var local = (LocalMetadata)instruction.Operand;
                    CreateAssignCall(instructions, methodInfo, methodBody, instruction, methodBody.Locals.IndexOf(local), invocationIdLocal);
                } else if (instruction.OpCode == OpCodes.Stloc_0) {
                    CreateAssignCall(instructions, methodInfo, methodBody, instruction, 0, invocationIdLocal);
                } else if (instruction.OpCode == OpCodes.Stloc_1) {
                    CreateAssignCall(instructions, methodInfo, methodBody, instruction, 1, invocationIdLocal);
                } else if (instruction.OpCode == OpCodes.Stloc_2) {
                    CreateAssignCall(instructions, methodInfo, methodBody, instruction, 2, invocationIdLocal);
                } else if (instruction.OpCode == OpCodes.Stloc_3) {
                    CreateAssignCall(instructions, methodInfo, methodBody, instruction, 3, invocationIdLocal);
                } else if (instruction.OpCode == OpCodes.Starg || instruction.OpCode == OpCodes.Starg_S) {
                    var parameter = (ParameterMetadata)instruction.Operand;
                    var parameterIndex = Array.IndexOf(methodInfo.Parameters, parameter);
                    
                    CreateAssignCall(instructions, methodInfo, methodBody, instruction, parameterIndex + methodBody.Locals.Count, invocationIdLocal);
                } else if (instruction.OpCode == OpCodes.Newobj && IsCompilerGenerated(((MethodBase)instruction.Operand).DeclaringType)) {
                    CreateRegisterClosureInstanceCall(instructions, instruction, invocationIdLocal);
                } else if (instruction.OpCode == OpCodes.Ret) {
                    CreateReturnCall(instructions, methodInfo, instruction, invocationIdLocal);
                }
            }
        }

        private static void CreateAssignCall(IlInstructionList instructions, VirtualMethodInfo methodInfo, VirtualMethodBody methodBody, IlInstruction assignInstruction, int slotIndex, LocalMetadata invocationIdLocal)
        {
            var result = new List<IlInstruction>();
            var localsCount = methodBody.Locals.Count;
            var slotName = slotIndex < localsCount
                ? methodBody.Locals[slotIndex].LocalName
                : slotIndex.ToString();

            if (slotName.Contains("<") || slotName.Contains("$")) {
                return;
            }

            result.Add(new IlInstruction(OpCodes.Dup, null, instructions));
            result.Add(assignInstruction);

            Type slotType;
            if (slotIndex < localsCount) {
                slotType = methodBody.Locals[slotIndex].LocalType;
            } else {
                slotType = methodInfo.Parameters[slotIndex - localsCount].ParameterType;
            }

            if (slotType.IsValueType) 
                result.Add(new IlInstruction(OpCodes.Box, slotType, instructions));

            result.Add(new IlInstruction(OpCodes.Ldloc, invocationIdLocal, instructions));
            result.Add(new IlInstruction(OpCodes.Ldc_I4, slotIndex, instructions));
            result.Add(new IlInstruction(OpCodes.Call, AssignMethod, instructions));
            
            assignInstruction.ReplaceWith(result);
        }

        private static void CreateRegisterClosureInstanceCall(IlInstructionList instructions, IlInstruction instruction, LocalMetadata invocationIdLocal)
        {
            instruction.ReplaceWith(new [] {
                instruction,
                new IlInstruction(OpCodes.Dup, null, instructions),
                new IlInstruction(OpCodes.Ldloc, invocationIdLocal, instructions),
                new IlInstruction(OpCodes.Call, RegisterClosureInstanceMethod, instructions)
            });
        }

        private static void CreateReturnCall(IlInstructionList instructions, VirtualMethodInfo metadata, IlInstruction instruction, LocalMetadata invocationIdLocal)
        {
            instruction.ReplaceWith(new List<IlInstruction> {
                new(OpCodes.Ldloc, invocationIdLocal, instructions),
                metadata.ReturnType != typeof(void)
                    ? new IlInstruction(OpCodes.Dup, null, instructions)
                    : new IlInstruction(OpCodes.Ldnull, null, instructions),
                new(OpCodes.Call, ReturnMethod, instructions),
                instruction
            });
        }

        private static void CreateLiveDebuggerStart(IlInstructionList instructions, VirtualMethodInfo methodInfo, string localNames, string paramNames, LocalMetadata invocationIdLocal)
        {
            var result = new List<IlInstruction>();
            if (methodInfo.IsStatic)
                result.Add(new IlInstruction(OpCodes.Ldnull, null, instructions));
            else
                result.Add(new IlInstruction(OpCodes.Ldarg_0, null, instructions));

            // call LiveDebuggerStart
            result.Add(new IlInstruction(OpCodes.Ldstr, methodInfo.MethodIdentifier, instructions));
            result.Add(new IlInstruction(OpCodes.Ldstr, localNames, instructions));
            result.Add(new IlInstruction(OpCodes.Ldstr, paramNames, instructions));

            // hasReturnValue
            if (methodInfo.ReturnType == typeof(void)) {
                result.Add(new IlInstruction(OpCodes.Ldc_I4_0, null, instructions));
            } else {
                result.Add(new IlInstruction(OpCodes.Ldc_I4_1, null, instructions));
            }

            // create `params object[]` argument
            result.Add(new IlInstruction(OpCodes.Ldc_I4, methodInfo.Parameters.Length, instructions));
            result.Add(new IlInstruction(OpCodes.Newarr, typeof(object), instructions));

            for (var i = 0; i < methodInfo.Parameters.Length; i++) {
                result.Add(new IlInstruction(OpCodes.Dup, null, instructions));
                result.Add(new IlInstruction(OpCodes.Ldc_I4, i, instructions));
                
                var argumentIndex = methodInfo.IsStatic ? i : i + 1;
                
                result.Add(new IlInstruction(OpCodes.Ldarg_S, argumentIndex, instructions));
                
                if (methodInfo.Parameters[i].ParameterType.IsValueType) {
                    result.Add(new IlInstruction(OpCodes.Box, methodInfo.Parameters[i].ParameterType, instructions));
                }
                
                result.Add(new IlInstruction(OpCodes.Stelem_Ref, null, instructions));
            }

            result.Add(new IlInstruction(OpCodes.Call, StartMethod, instructions));
            result.Add(new IlInstruction(OpCodes.Stloc, invocationIdLocal, instructions));

            var head = instructions.Head;

            result.Add(head);

            head.ReplaceWith(result);
        }

        private bool OperandIsTarget(IlInstruction ilInstruction)
        {
            return ilInstruction.OpCode.FlowControl == FlowControl.Branch || ilInstruction.OpCode.FlowControl == FlowControl.Cond_Branch;
        }
    
        private static bool IsCompilerGenerated(Type type)
        {
            return type.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
        }
        
        private bool IsCompilerGenerated(MethodBase method)
        {
            return method.GetCustomAttribute<CompilerGeneratedAttribute>() != null;
        }
    }
}