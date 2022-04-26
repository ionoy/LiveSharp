using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Virtual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Xml.Linq;

namespace LiveSharp.Runtime.IL
{
    public static class Devirtualizer
    {
        private static readonly MethodInfo Stfld = typeof(VirtualClr).GetMethod(nameof(VirtualClr.Stfld));
        private static readonly MethodInfo Stsfld = typeof(VirtualClr).GetMethod(nameof(VirtualClr.Stsfld));
        private static readonly MethodInfo Ldfld = typeof(VirtualClr).GetMethod(nameof(VirtualClr.Ldfld));
        private static readonly MethodInfo Ldsfld = typeof(VirtualClr).GetMethod(nameof(VirtualClr.Ldsfld));
        private static readonly MethodInfo Ldflda = typeof(VirtualClr).GetMethod(nameof(VirtualClr.Ldflda));
        private static readonly MethodInfo Ldsflda = typeof(VirtualClr).GetMethod(nameof(VirtualClr.Ldsflda));
        private static readonly MethodInfo ResolveDelegate = typeof(VirtualClr).GetMethod(nameof(VirtualClr.ResolveDelegate));
        private static readonly MethodInfo ResolveGenericDelegate = typeof(VirtualClr).GetMethod(nameof(VirtualClr.ResolveGenericDelegate));
        private static readonly MethodInfo ResolveMethodMetadata = typeof(VirtualClr).GetMethod(nameof(VirtualClr.ResolveMethodMetadata));
        private static readonly MethodInfo CreateDelegate = typeof(VirtualClr).GetMethod(nameof(VirtualClr.CreateDelegate));
        private static readonly MethodInfo GetUninitializedObject = typeof(FormatterServices).GetMethod(nameof(FormatterServices.GetUninitializedObject));
        private static readonly MethodInfo GetTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));
        private static readonly FieldInfo IntPtrZero = typeof(IntPtr).GetField(nameof(IntPtr.Zero));
        private static readonly FieldInfo MoveNextToken = typeof(VirtualAsyncStateMachine).GetField(nameof(VirtualAsyncStateMachine._moveNextToken));

        public static IlInstructionList Devirtualize(VirtualMethodBody methodBody, IlInstructionList instructions, ILiveSharpRuntimeExt extensions)
        {
            // don't rewrite ldflda if we are stuck with Expression Tree compiler
            // there are lots of issues with ET and by-ref types  
            var rewriteLdflda = extensions?.IsDynamicMethodSupported() == true;
            
            foreach (var instruction in instructions) {
                var opCode = instruction.OpCode;
                var operand = instruction.Operand;

                // if (opCode == OpCodes.Newobj && operand is MethodBase mb && mb.DeclaringType.ResolveVirtualType() == typeof(VirtualAsyncStateMachine)) {
                //     var vti = (VirtualTypeInfo)mb.DeclaringType;
                //     var moveNextMethod = vti.VirtualMethods.FirstOrDefault(m => m.Name == "MoveNext");
                //     if (moveNextMethod == null)
                //         throw new InvalidOperationException($"Couldn't find MoveNext method on {vti}");
                //     
                //     instruction.ReplaceWith(new[] {
                //         instruction,
                //         new IlInstruction(OpCodes.Dup, null, instructions),
                //         new IlInstruction(OpCodes.Ldc_I4, moveNextMethod.Token, instructions),
                //         new IlInstruction(OpCodes.Stfld, MoveNextToken, instructions),
                //     });
                // } else 
                if (opCode == OpCodes.Ldftn || opCode == OpCodes.Ldvirtftn) {
                    // ldftn is converted to two values
                    // first is IntPtr (pointer if non-virtual, and zero if virtual)
                    // second is MethodMetadata (null if non-virtual, and instance if virtual)
                    if (operand is VirtualMethodInfo vmi) {
                        instruction.ReplaceWith(new [] {
                            new IlInstruction(OpCodes.Ldsfld,IntPtrZero, instructions),
                            new IlInstruction(OpCodes.Ldc_I4, vmi.Token, instructions),
                            new IlInstruction(OpCodes.Call, ResolveMethodMetadata, instructions, comment: vmi.MethodIdentifier)
                        });
                    } else {
                        instruction.ReplaceWith(new [] {
                            instruction,
                            new IlInstruction(OpCodes.Ldnull, null, instructions, "DelegateBuilder is null for ldftn")
                        });
                    }
                } else if (IsDelegateConstructor(instruction, out var delegateType)) {
                    RewriteDelegateConstructor(instructions, instruction, delegateType);
                } else 
                if (operand is VirtualMethodInfo vmi) {
                    if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt || opCode == OpCodes.Newobj)
                        RewriteCallOrCtor(methodBody, instructions, vmi, instruction);
                } else if (operand is VirtualFieldInfo vfi) {
                    var token = vfi.Token;
                    var fieldType = CompilerHelpers.ResolveVirtualType(vfi.FieldType);
                    
                    if (opCode == OpCodes.Ldfld) {
                        var ldfldInstance = Ldfld.MakeGenericMethod(fieldType);
                        
                        if (vfi.DeclaringType.ContainsGenericParameters) {
                            var a = vfi;
                        }
                        
                        
                        instruction.ReplaceWith(new [] {
                            new IlInstruction(OpCodes.Ldc_I4, token, instructions),
                            new IlInstruction(OpCodes.Call, ldfldInstance, instructions)
                        });
                        
                        continue;
                    }

                    if (opCode == OpCodes.Ldsfld) {
                        var ldfldInstance = Ldsfld.MakeGenericMethod(fieldType);
                        
                        instruction.ReplaceWith(new [] {
                            new IlInstruction(OpCodes.Ldc_I4, token, instructions),
                            new IlInstruction(OpCodes.Call, ldfldInstance, instructions)
                        });
                        
                        continue;
                    }

                    if (rewriteLdflda && opCode == OpCodes.Ldflda) {
                        var ldfldaInstance = Ldflda.MakeGenericMethod(fieldType);
                        
                        instruction.ReplaceWith(new [] {
                            new IlInstruction(OpCodes.Ldc_I4, token, instructions),
                            new IlInstruction(OpCodes.Call, ldfldaInstance, instructions)
                        });
                        
                        continue;
                    }

                    if (rewriteLdflda && opCode == OpCodes.Ldsflda) {
                        var ldsfldaInstance = Ldsflda.MakeGenericMethod(fieldType);
                        
                        instruction.ReplaceWith(new [] {
                            new IlInstruction(OpCodes.Ldc_I4, token, instructions),
                            new IlInstruction(OpCodes.Call, ldsfldaInstance, instructions)
                        });
                        
                        continue;
                    }

                    if (opCode == OpCodes.Stfld) {
                        var stfldInstance = Stfld.MakeGenericMethod(fieldType);
                        
                        instruction.ReplaceWith(new [] {
                            new IlInstruction(OpCodes.Ldc_I4, token, instructions),
                            new IlInstruction(OpCodes.Call, stfldInstance, instructions)
                        });
                        
                        continue;
                    }

                    if (opCode == OpCodes.Stsfld) {
                        var stfldInstance = Stsfld.MakeGenericMethod(fieldType);
                        
                        instruction.ReplaceWith(new [] {
                            new IlInstruction(OpCodes.Ldc_I4, token, instructions),
                            new IlInstruction(OpCodes.Call, stfldInstance, instructions)
                        });
                    }
                } else if (operand is VirtualTypeInfo vti) {
                    instruction.Operand = vti.ResolveVirtualType();
                }
            }

            return instructions;
        }

        private static bool IsDelegateConstructor(IlInstruction instruction, out Type delegateType)
        {
            if (instruction.OpCode == OpCodes.Newobj && instruction.Operand is MethodBase mi && mi.DeclaringType.IsDelegate()) {
                delegateType = mi.DeclaringType.ResolveVirtualType();
                return true;
            }

            if (instruction.OpCode == OpCodes.Newobj && instruction.Operand is GenericTypeResolverEval eval && eval.UnresolvedType.IsDelegate()) {
                delegateType = eval.UnresolvedType;
                return true;
            }

            delegateType = null;
            return false;
        }

        private static void RewriteDelegateConstructor(IlInstructionList instructions, IlInstruction instruction, Type delegateType)
        {
            var virtualClrCreateDelegate = new IlInstruction(OpCodes.Call, CreateDelegate.MakeGenericMethod(delegateType), instructions);
            
            // we expect IntPtr + DelegateBuilder in place of just IntPtr
            instruction.ReplaceWith(virtualClrCreateDelegate); 
        }

        private static void RewriteCallOrCtor(VirtualMethodBody methodBody, IlInstructionList instructions, VirtualMethodInfo vmi, IlInstruction instruction)
        {
            var invokeMethod = getDelegateInvokeMethod();
            var resultInstructions = new List<IlInstruction>();
            var parameters = vmi.GetParameters().ToList();
            // We skip object instance for constructors
            //var invokeParameters = invokeMethod.GetParameters().Skip(instruction.OpCode == OpCodes.Newobj ? 1 : 0);

            // we need a call to delegate
            // what to we do when it's generic?
            // look into the build-time code I guess
            
            if (instruction.OpCode != OpCodes.Newobj && !vmi.IsStatic) {
                parameters.Insert(0, new VirtualParameterInfo(vmi.DeclaringType.ResolveVirtualType(), "$this"));
            }
            
            var argumentStorage = parameters
                .Select((p, i) => new LocalMetadata("$" + vmi.Name + i, p.ParameterType.ResolveVirtualType()))
                .ToArray();
            
            addLocals(argumentStorage);

            if (instruction.OpCode == OpCodes.Newobj) {
                var declaringType = vmi.DeclaringType ?? throw new InvalidOperationException($"DeclaringType for {vmi} is not set");
                resultInstructions.AddRange(popArguments(argumentStorage));
                resultInstructions.AddRange(pushUninitializedObject(declaringType));
                resultInstructions.Add(storeUninitializedObject(declaringType, out var objectLocal));
                resultInstructions.AddRange(pushMethodDelegate());
                resultInstructions.Add(new IlInstruction(OpCodes.Ldloc, objectLocal, instructions));
                resultInstructions.AddRange(pushArguments(argumentStorage));
                resultInstructions.Add(pushDelegateInvokeCall());
            } else {
                resultInstructions.AddRange(popArguments(argumentStorage));
                resultInstructions.AddRange(pushMethodDelegate());
                resultInstructions.AddRange(pushArguments(argumentStorage));
                resultInstructions.Add(pushDelegateInvokeCall());
            }
                
            instruction.ReplaceWith(resultInstructions);

            IEnumerable<IlInstruction> popArguments(LocalMetadata[] newLocals)
            {
                // pop original arguments into new locals
                foreach (var local in newLocals.Reverse())
                     yield return new IlInstruction(OpCodes.Stloc, local, instructions);
            }

            void addLocals(LocalMetadata[] newLocals)
            {
                // add new locals to method metadata
                foreach (var local in newLocals)
                    methodBody.Locals.Add(local);
            }

            IReadOnlyList<IlInstruction> pushMethodDelegate()
            {
                if (vmi.ContainsGenericParameters) {
                    var resolveGenericVirtualMethodToken = new GenericTypeResolverEval(resolver => {
                        var resolvedVmi = (VirtualMethodInfo)resolver.ApplyGenericArguments(vmi);
                        return resolvedVmi.Token;
                    });

                    return new[] {
                        new IlInstruction(OpCodes.Ldc_I4, resolveGenericVirtualMethodToken, instructions),
                        new IlInstruction(OpCodes.Call, ResolveDelegate, instructions, comment: vmi.MethodIdentifier),
                    };
                }
                
                // load virtual method delegate on the stack
                return new[] {
                    new IlInstruction(OpCodes.Ldc_I4, vmi.Token, instructions),
                    new IlInstruction(OpCodes.Call, ResolveDelegate, instructions, comment: vmi.MethodIdentifier),
                };
            }

            IEnumerable<IlInstruction> pushArguments(LocalMetadata[] newLocals)
            {
                // push original arguments back on stack
                foreach (var local in newLocals)
                    yield return new IlInstruction(OpCodes.Ldloc, local, instructions);
            }

            IEnumerable<IlInstruction> pushUninitializedObject(Type declaringType)
            {
                return new[] {
                    new IlInstruction(OpCodes.Ldtoken, declaringType.ResolveVirtualType(), instructions),
                    new IlInstruction(OpCodes.Call, GetTypeFromHandle, instructions),
                    new IlInstruction(OpCodes.Call, GetUninitializedObject, instructions),
                    new IlInstruction(OpCodes.Dup, null, instructions)
                };
            }

            IlInstruction storeUninitializedObject(Type declaringType, out LocalMetadata objectLocal)
            {
                objectLocal = new LocalMetadata("$uninitializedObject", declaringType.ResolveVirtualType());
                methodBody.Locals.Add(objectLocal);

                return new IlInstruction(OpCodes.Stloc, objectLocal, instructions);
            }

            IlInstruction pushDelegateInvokeCall()
            {
                return new(OpCodes.Callvirt, invokeMethod, instructions);
            }

            object getDelegateInvokeMethod()
            {
                if (vmi.ContainsGenericParameters) {
                    if (vmi.GenericArguments.Count == vmi.GenericParameters.Length && !vmi.GenericArguments.OfType<GenericTypeParameter>().Any()) {
                        // If we have all the required info to create the delegate
                        vmi = vmi.MakeGenericMethod();
                    } else {
                        return new GenericTypeResolverEval(resolver => {
                            var delegateType = getVmiDelegateType(resolver);
                            return delegateType.GetMethod("Invoke");
                        });
                    }
                }
                
                return vmi.DelegateBuilder.DelegateType.GetMethod("Invoke") 
                       ?? throw new InvalidOperationException($"Invoke method not found on {vmi.DelegateBuilder.DelegateType}");
            }
            
            Type getVmiDelegateType(GenericTypeResolver resolver)
            {
                var parameterTypes = vmi.Parameters.Select(p => resolver.ResolveGenericType(p.ParameterType));
                var returnType = resolver.ResolveGenericType(vmi.ReturnType);
                var delegateType = LiveSharpAssemblyContext.GetDelegateType(vmi.IsStatic, parameterTypes, returnType);
                return delegateType;
            }
        }
    }

    public class GenericTypeResolverEval
    {
        public Type UnresolvedType { get; }

        private readonly Func<GenericTypeResolver, object> _evaluation;
        public GenericTypeResolverEval(Func<GenericTypeResolver, object> evaluation, Type unresolvedType = null)
        {
            _evaluation = evaluation;
            UnresolvedType = unresolvedType;
        }

        public object Evaluate(GenericTypeResolver resolver) => _evaluation(resolver);
    }
}