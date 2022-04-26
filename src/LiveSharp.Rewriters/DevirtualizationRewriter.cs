using Mono.Cecil;
using Mono.Cecil.Cil;
using System;

namespace LiveSharp.Rewriters
{
    // public class DevirtualizationRewriter
    // {
    //     private readonly AssemblyDefinition _assembly;
    //     private readonly AssemblyDiff _diff;
    //     private readonly ILogger _logger;
    //     private LiveSharpRuntimeMethods _runtimeMethods;
    //
    //     public DevirtualizationRewriter(AssemblyDefinition assembly, AssemblyDiff diff, ILogger logger)
    //     {
    //         _assembly = assembly;
    //         _diff = diff;
    //         _logger = logger;
    //     }
    //
    //     public void Rewrite()
    //     {
    //         //TODO
    //         return;
    //         _runtimeMethods = LiveSharpRuntimeMethods.LocateRuntimeMethods(_assembly, _logger);
    //
    //         foreach (var method in _diff.NewMethods) {
    //             Rewrite(method);
    //         }
    //     }
    //
    //     private void Rewrite(MethodDefinition method)
    //     {
    //         if (!method.HasBody)
    //             return;
    //         
    //         var instructions = method.Body.Instructions;
    //         var processor = method.Body.GetILProcessor();
    //         
    //         for (int i = 0; i < instructions.Count; i++) {
    //             var instruction = instructions[i];
    //
    //             if (instruction.Operand is FieldReference fr && _diff.NewFields.Contains(fr.Resolve())) {
    //                 RewriteVirtualFieldInstruction(instruction, processor);
    //             }
    //
    //             if (instruction.Operand is MethodReference mr && _diff.NewMethods.Contains(mr.Resolve())) {
    //                 RewriteVirtualMethodInstruction(instruction, processor);
    //             }
    //         }
    //     }
    //
    //     private void RewriteVirtualMethodInstruction(Instruction instruction, ILProcessor processor)
    //     {
    //         if (instruction.OpCode == OpCodes.Ldftn) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Newobj) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Call) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Callvirt) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Ldtoken) {
    //             
    //         } else {
    //             throw new NotImplementedException(instruction.ToString());
    //         }
    //     }
    //
    //     private void RewriteVirtualFieldInstruction(Instruction instruction, ILProcessor processor)
    //     {
    //         if (instruction.OpCode == OpCodes.Ldfld) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Stfld) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Ldsfld) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Stsfld) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Ldflda) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Ldsflda) {
    //             
    //         } else if (instruction.OpCode == OpCodes.Ldtoken) {
    //             
    //         } else {
    //             throw new NotImplementedException(instruction.ToString());
    //         }
    //     }
    // }
    // class LiveSharpRuntimeMethods
    // {
    //
    //     public static LiveSharpRuntimeMethods LocateRuntimeMethods(AssemblyDefinition assembly, ILogger logger)
    //     {
    //         var methods = new LiveSharpRuntimeMethods();
    //         var runtimeReference = assembly.MainModule.AssemblyReferences.FirstOrDefault(r => r.Name == "LiveSharp.Runtime");
    //         var runtimeAssemblyDefinition = assembly.MainModule.AssemblyResolver.Resolve(runtimeReference);
    //
    //         if (runtimeAssemblyDefinition.MainModule.TryGetTypeReference("LiveSharp.VirtualClr", out var virtualClrType)) {
    //             foreach (var method in virtualClrType.Resolve().Methods) {
    //                 
    //             }
    //         } else {
    //             logger.LogError("Unable to find VirtualClr type in LiveSharp.Runtime");
    //         }
    //
    //         return methods;
    //     }
    //     
    // }
}