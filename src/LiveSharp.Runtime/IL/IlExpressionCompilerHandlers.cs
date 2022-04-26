using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Virtual;

// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo

namespace LiveSharp.Runtime.IL
{
    public static class IlExpressionCompilerHandlers
    {
        private static readonly MethodInfo InitobjMethod = typeof(VirtualClr).GetMethod(nameof(VirtualClr.Initobj));
        
        public static Expression Ldnull(InstructionContext ctx) => Expression.Constant(null);

        public static Expression Newarr(InstructionContext ctx)
        {
            var arraySize = ctx.PopStack();
            var arrayType = (Type)ctx.Instruction.Operand;

            return Expression.NewArrayBounds(arrayType, new[] {arraySize});
        }

        public static Expression Dup(InstructionContext ctx) => ctx.PeekStack();

        public static Expression Ldtoken(InstructionContext ctx)
        {
            if (ctx.Instruction.Operand is VirtualFieldInfo vfi) 
                return Expression.Constant(vfi);
            if (ctx.Instruction.Operand is FieldInfo fieldInfo)
                return Expression.Constant(fieldInfo.FieldHandle);
            if (ctx.Instruction.Operand is MethodInfo methodInfo)
                return Expression.Constant(methodInfo.MethodHandle);
            if (ctx.Instruction.Operand is Type type)
                return Expression.Constant(type.TypeHandle);

            throw new InvalidOperationException($"Invalid operand '{ctx.Instruction.Operand}' for Ldtoken");
        }

        public static Expression Ldstr(InstructionContext ctx) =>
            Expression.Constant((string)ctx.Instruction.Operand);

        public static Expression Ldc_I4_M1(InstructionContext ctx) => Expression.Constant(-1);

        public static Expression Ldc_I4(InstructionContext ctx) => 
            Expression.Constant(ctx.Instruction.Operand is int i 
                ? i 
                : int.Parse((string)ctx.Instruction.Operand));
        
        public static Expression Ldc_I4_S(InstructionContext ctx) => Ldc_I4(ctx);
        public static Expression Ldc_I4_0(InstructionContext ctx) => Expression.Constant(0);
        public static Expression Ldc_I4_1(InstructionContext ctx) => Expression.Constant(1);
        public static Expression Ldc_I4_2(InstructionContext ctx) => Expression.Constant(2);
        public static Expression Ldc_I4_3(InstructionContext ctx) => Expression.Constant(3);
        public static Expression Ldc_I4_4(InstructionContext ctx) => Expression.Constant(4);
        public static Expression Ldc_I4_5(InstructionContext ctx) => Expression.Constant(5);
        public static Expression Ldc_I4_6(InstructionContext ctx) => Expression.Constant(6);
        public static Expression Ldc_I4_7(InstructionContext ctx) => Expression.Constant(7);
        public static Expression Ldc_I4_8(InstructionContext ctx) => Expression.Constant(8);

        public static Expression Ldc_R4(InstructionContext ctx) =>
            Expression.Constant(float.Parse(ctx.Instruction.Operand.ToString()), typeof(float));

        public static Expression Ldc_R8(InstructionContext ctx) =>
            Expression.Constant(double.Parse(ctx.Instruction.Operand.ToString()), typeof(double));

        public static Expression Stloc_0(InstructionContext ctx) =>
            CompilerHelpers.AssignCoerce(ctx.Compiler.Locals[0], ctx.PopStack());

        public static Expression Stloc_1(InstructionContext ctx) =>
            CompilerHelpers.AssignCoerce(ctx.Compiler.Locals[1], ctx.PopStack());

        public static Expression Stloc_2(InstructionContext ctx) =>
            CompilerHelpers.AssignCoerce(ctx.Compiler.Locals[2], ctx.PopStack());

        public static Expression Stloc_3(InstructionContext ctx) =>
            CompilerHelpers.AssignCoerce(ctx.Compiler.Locals[3], ctx.PopStack());

        public static Expression Stloc(InstructionContext ctx) =>
            CompilerHelpers.AssignCoerce( ctx.Compiler.LocalMap[(LocalMetadata)ctx.Instruction.Operand], ctx.PopStack());

        public static Expression Stloc_S(InstructionContext ctx) => Stloc(ctx);

        public static Expression Ldloc_0(InstructionContext ctx) => ctx.Compiler.Locals[0];

        public static Expression Ldloc_1(InstructionContext ctx) => ctx.Compiler.Locals[1];
        public static Expression Ldloc_2(InstructionContext ctx) => ctx.Compiler.Locals[2];
        public static Expression Ldloc_3(InstructionContext ctx) => ctx.Compiler.Locals[3];
        public static Expression Ldloc(InstructionContext ctx) => ctx.Compiler.LocalMap[(LocalMetadata)ctx.Instruction.Operand];
        public static Expression Ldloc_S(InstructionContext ctx) => Ldloc(ctx);
        public static Expression Ldloca(InstructionContext ctx) => ctx.Compiler.LocalMap[(LocalMetadata)ctx.Instruction.Operand];
        public static Expression Ldloca_S(InstructionContext ctx) => Ldloca(ctx);
        public static Expression Ldarg_0(InstructionContext ctx) => ctx.Compiler.CastedThis ?? ctx.Compiler.GetArgumentByIndex(0);
        public static Expression Ldarg_1(InstructionContext ctx) => ctx.Compiler.GetArgumentByIndex(1);
        public static Expression Ldarg_2(InstructionContext ctx) => ctx.Compiler.GetArgumentByIndex(2);
        public static Expression Ldarg_3(InstructionContext ctx) => ctx.Compiler.GetArgumentByIndex(3);
        public static Expression Ldarg(InstructionContext ctx) => ctx.Compiler.GetArgumentByIndex(int.Parse(ctx.Instruction.Operand.ToString()));
        public static Expression Ldarga(InstructionContext ctx) => ctx.Compiler.GetArgumentByIndex(int.Parse(ctx.Instruction.Operand.ToString()));
        public static Expression Ldarg_S(InstructionContext ctx) => ctx.Compiler.GetArgumentByIndex(int.Parse(ctx.Instruction.Operand.ToString()));
        public static Expression Ldarga_S(InstructionContext ctx) => ctx.Compiler.GetArgumentByIndex(int.Parse(ctx.Instruction.Operand.ToString()));
        public static Expression Callvirt(InstructionContext ctx) => CallImpl(ctx, true);
        public static Expression Call(InstructionContext ctx) => CallImpl(ctx);

        public static Expression CallImpl(InstructionContext ctx, bool isVirtualCall = false)
        {
            // TODO: callvirt
            var methodBase = (MethodBase)ctx.Instruction.Operand;
            
            var parameters = methodBase.GetParameters();
            var arguments = CompilerHelpers.GetArguments(ctx, parameters);

            if (methodBase.DeclaringType == typeof(System.Runtime.CompilerServices.RuntimeHelpers) && 
                methodBase.Name == nameof(System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray)) {

                if (arguments.Length == 2 && arguments[1].Type == typeof(VirtualFieldInfo)) {
                    methodBase = CompilerHelpers.InitializeArrayMethod;
                    parameters = methodBase.GetParameters();
                }
            }

            if (methodBase.DeclaringType == typeof(FieldInfo) &&
                methodBase.Name == nameof(FieldInfo.GetFieldFromHandle)) {
                if (arguments.Length > 0 && arguments[0].Type == typeof(VirtualFieldInfo)) {
                    return arguments[0];
                }
            }
            
            CompilerHelpers.CoerceArguments(arguments, parameters);

            if (methodBase is MethodInfo mi) {
                if (!methodBase.IsStatic) {
                    var thisConstant = ctx.PopStack();
                    
                    if (thisConstant.Type.HasBaseType(mi.DeclaringType.FullName) && !isVirtualCall) {
                        // if it has overload, we still need to call base
                        if (thisConstant.Type.FindDeclaredMethod(mi.Name, parameters) != null) {
                            return createBaseCall(CompilerHelpers.Coerce(thisConstant, mi.DeclaringType), mi, arguments);
                        }
                    }

                    if (thisConstant.Type.IsByRef) {
                        return new ByRefCall(thisConstant, mi, arguments);
                    }
                    
                    // ignore byref conversions
                    //if (!(thisConstant.Type.IsByRef && thisConstant.Type.GetElementType() == mi.DeclaringType))
                    thisConstant = CompilerHelpers.Coerce(thisConstant, mi.DeclaringType);

                    if (mi is VirtualMethodInfo virtualMethodInfo)
                        return CompilerHelpers.CreateCallVirtualMethodExpression(thisConstant, virtualMethodInfo, arguments);
                    
                    return Expression.Call(thisConstant, mi, arguments);
                }

                if (mi is VirtualMethodInfo vm)
                    return CompilerHelpers.CreateCallVirtualMethodExpression(Expression.Constant(null), vm, arguments);


                return Expression.Call(mi, arguments);
            }

            if (methodBase is ConstructorInfo constructorInfo) {
                var thisConstant = ctx.PopStack();
                if (thisConstant.Type.HasBaseType(constructorInfo.DeclaringType?.FullName)) {
                    var args = Expression.NewArrayInit(typeof(object), arguments.Select(a => Expression.Convert(a, typeof(object))));

                    // if (constructorInfo is VirtualConstructorInfo vci)
                    //     return Expression.Call(RuntimeHelpers.CallBaseConstructorVirtualMethod.Value, Expression.Constant(vci.Invoker), thisConstant, args);
                    
                    return Expression.Call(RuntimeHelpers.CallBaseConstructorWithInfoMethod.Value, Expression.Constant(constructorInfo), thisConstant, args);
                }
                
                return Expression.Assign(thisConstant,Expression.New(constructorInfo, arguments));
            }

            Expression createBaseCall(Expression target, MethodInfo methodToCall, Expression[] arguments)
            {
                var args = Expression.NewArrayInit(typeof(object), (arguments.Select(e => Expression.Convert(e, typeof(object)))));
                
                if (methodToCall.ReturnType == typeof(void))
                    return Expression.Call(RuntimeHelpers.CallBaseMethodVoidMethod.Value, Expression.Constant(methodToCall), target, args);

                return CompilerHelpers.Coerce(Expression.Call(RuntimeHelpers.CallBaseMethodReturningMethod.Value, Expression.Constant(methodToCall), target, args), mi.ReturnType);
            }
            
            throw new InvalidOperationException("Supplied method is not MethodInfo or ConstructorInfo");
        }

        public static Expression Nop(InstructionContext ctx) => Expression.Empty();

        public static Expression Ret(InstructionContext ctx)
        {
            if (CompilerHelpers.IsSameType(ctx.Compiler.Method.ReturnType, typeof(void)))
                return Expression.Empty();
            return ctx.PopStack();
        }

        public static Expression Brtrue_S(InstructionContext ctx)
        {
            var condition = ctx.PopStack();
            var labelTarget = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index, ((IlInstruction)ctx.Instruction.Operand).Index, true);

            if (condition.Type != typeof(bool)) {
                condition = Expression.NotEqual(condition, Expression.Default(condition.Type));
            }

            return Expression.IfThen(condition, Expression.Goto(labelTarget));
        }

        public static Expression Brtrue(InstructionContext ctx) => Brtrue_S(ctx);

        public static Expression Brfalse_S(InstructionContext ctx)
        {
            var condition = ctx.PopStack();
            var labelTarget = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index, ((IlInstruction)ctx.Instruction.Operand).Index, true);

            condition = CompilerHelpers.Coerce(condition, typeof(bool));

            return Expression.IfThen(Expression.Not(condition), Expression.Goto(labelTarget));
        }

        public static Expression Brfalse(InstructionContext ctx) => Brfalse_S(ctx);

        public static Expression Beq(InstructionContext ctx)
        {
            var test = Ceq(ctx);
            var labelTarget = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index,((IlInstruction)ctx.Instruction.Operand).Index, true);

            return Expression.IfThen(test, Expression.Goto(labelTarget));
        }

        public static Expression Beq_S(InstructionContext ctx) => Beq(ctx);

        public static Expression Br(InstructionContext ctx)
        {
            var labelTarget = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index,((IlInstruction)ctx.Instruction.Operand).Index, false);
            return Expression.Goto(labelTarget);
        }

        public static Expression Br_S(InstructionContext ctx) => Br(ctx);

        public static Expression Leave(InstructionContext ctx)
        {
            ctx.EmptyStack();

            var labelTarget = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index,((IlInstruction)ctx.Instruction.Operand).Index, false, true);
            
            return Expression.Goto(labelTarget);
        }

        public static Expression Leave_S(InstructionContext ctx) => Leave(ctx);

        public static Expression Clt(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();
            var coercedLeft = CompilerHelpers.Coerce(left, right.Type);
            return Expression.Condition(Expression.LessThan(coercedLeft, right), Expression.Constant(1),
                Expression.Constant(0));
        }

        public static Expression Clt_Un(InstructionContext ctx) => Clt(ctx);

        public static Expression Cgt(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();

            // object comparisons to null work with Cgt sometimes
            if (!left.Type.IsValueType)
                left = CompilerHelpers.Coerce(left, typeof(int));
            
            if (!right.Type.IsValueType)
                right = CompilerHelpers.Coerce(right, typeof(int));

            if (!CompilerHelpers.IsArithmetic(left.Type))
                left = CompilerHelpers.Coerce(left, typeof(int));
            
            if (!CompilerHelpers.IsArithmetic(right.Type))
                right = CompilerHelpers.Coerce(right, typeof(int));
            
            return Expression.Condition(Expression.GreaterThan(left, right), Expression.Constant(1),
                Expression.Constant(0));
        }

        public static Expression Cgt_Un(InstructionContext ctx) => Cgt(ctx);

        public static Expression Ceq(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();

            if (left.Type == typeof(bool) && right.Type == typeof(int))
                right = CompilerHelpers.CoerceIntToBool(right);

            if (right.Type == typeof(bool) && left.Type == typeof(int))
                left = CompilerHelpers.CoerceIntToBool(left);

            if (left.Type.IsEnum && right.Type == typeof(int))
                left = Expression.Convert(left, typeof(int));

            if (right.Type.IsEnum && left.Type == typeof(int))
                right = Expression.Convert(right, typeof(int));

            return Expression.Equal(left, right);
        }

        public static Expression Sub_Ovf_Un(InstructionContext ctx) =>
            Sub_Ovf(ctx);

        public static Expression Sub_Ovf(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();

            (left, right) = CompilerHelpers.EnsureArithmeticBinary(left, right);
            
            return Expression.SubtractChecked(left, right);
        }

        public static Expression Sub(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();
            
            (left, right) = CompilerHelpers.EnsureArithmeticBinary(left, right);
            
            return Expression.Subtract(left, right);
        }

        public static Expression Add(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();
            
            (left, right) = CompilerHelpers.EnsureArithmeticBinary(left, right);
            
            return Expression.Add(left, right);
        }

        public static Expression Add_Ovf(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();
            
            (left, right) = CompilerHelpers.EnsureArithmeticBinary(left, right);
            
            return Expression.AddChecked(left, right);
        }

        public static Expression Add_Ovf_Un(InstructionContext ctx) =>
            Add_Ovf(ctx);
        public static Expression Initobj(InstructionContext ctx)
        {
            var nextInstruction = ctx.PopStack();
            var valueType = nextInstruction.Type;

            // ET can't pass byref typed value to byref parameter................
            if (valueType.IsByRef) {
                return Expression.Empty();
            }
            
            var initObjTypeArg = valueType.IsByRef ? valueType.GetElementType() : valueType;
            var constructedInitObj = InitobjMethod.MakeGenericMethod(initObjTypeArg);
            
            
            return Expression.Call(constructedInitObj, nextInstruction);
        }

        public static Expression Newobj(InstructionContext ctx)
        {
            if (ctx.Instruction.Operand is VirtualMethodInfo virtualConstructorInfo) {
                var parameters = virtualConstructorInfo.GetParameters();
                var arguments = CompilerHelpers.GetArguments(ctx, parameters);
                
                CompilerHelpers.CoerceArguments(arguments, parameters);
                
                var argumentsArray = Expression.NewArrayInit(typeof(object), arguments.Select(e => Expression.Convert(e, typeof(object))));
                var invoker = virtualConstructorInfo.Invoker;

                return Expression.Call(Expression.Constant(invoker),
                    nameof(VirtualInvoker.InvokeConstructor),
                    new [] { virtualConstructorInfo.DeclaringType.UnderlyingSystemType },
                    argumentsArray);
            }
            else {
                var ctorInfo = (ConstructorInfo)ctx.Instruction.Operand;
                var parameters = ctorInfo.GetParameters();
                var arguments = new Expression[parameters.Length];//CompilerHelpers.GetArguments(ctx, parameters);
                var isDelegate = ctorInfo.DeclaringType.IsDelegate();
                
                for (int i = 0; i < parameters.Length; i++) {
                    var (expr, metadata) = ctx.PopStackWithMetadata();
                    if (isDelegate && metadata is VirtualMethodInfo vmi) {
                        arguments[parameters.Length - i - 1] = Expression.Constant(vmi.DelegateBuilder);
                    } else {
                        arguments[parameters.Length - i - 1] = expr;
                    }
                }
                
                if (isDelegate && arguments.Length == 2) {
                    var target = arguments[0];
                    var methodPointer = arguments[1];

                    if (methodPointer is ConstantExpression c && c.Type == typeof(DelegateBuilder)) {
                        var createDelegateCall = Expression.Call(methodPointer, nameof(DelegateBuilder.CreateWrappedDelegateWithInstance), new Type[0], target, Expression.Constant(ctorInfo.DeclaringType));
                        return Expression.Convert(createDelegateCall, ctorInfo.DeclaringType.ResolveVirtualType());
                    }
                }
                
                CompilerHelpers.CoerceArguments(arguments, parameters);

                return Expression.New(ctorInfo, arguments);
            }
        }
        
        public static Expression Stelem(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var index = ctx.PopStack();
            var array = ctx.PopStack();

            var arrayType = array.Type.IsArray
                ? array.Type.GetElementType()
                : throw new Exception("Array type is not array: " + array.Type);
            
            return Expression.Assign(Expression.ArrayAccess(array, index), CompilerHelpers.Coerce(value, arrayType));
        }

        public static Expression Stelem_I(InstructionContext ctx) => Stelem(ctx);

        public static Expression Stelem_I1(InstructionContext ctx) => Stelem(ctx);

        public static Expression Stelem_I2(InstructionContext ctx) => Stelem(ctx);

        public static Expression Stelem_I4(InstructionContext ctx) => Stelem(ctx);

        public static Expression Stelem_I8(InstructionContext ctx) => Stelem(ctx);

        public static Expression Stelem_R4(InstructionContext ctx) => Stelem(ctx);

        public static Expression Stelem_R8(InstructionContext ctx) => Stelem(ctx);

        public static Expression Stelem_Ref(InstructionContext ctx) => Stelem(ctx);

        public static Expression Ldelem(InstructionContext ctx)
        {
            var index = ctx.PopStack();
            var array = ctx.PopStack();

            return Expression.ArrayIndex(array, index);
        }

        public static Expression Ldelema(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_I(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_I1(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_I2(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_I4(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_I8(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_R4(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_R8(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_Ref(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_U1(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_U2(InstructionContext ctx) => Ldelem(ctx);
        public static Expression Ldelem_U4(InstructionContext ctx) => Ldelem(ctx);

        public static Expression Conv_I(InstructionContext ctx)
        {
            var val = ctx.PopStack();

            if (val.Type == typeof(string))
                val = Expression.Call(CompilerHelpers.StringToPointerMethod, val);
            
            return Expression.Convert(val, typeof(IntPtr));
        }

        public static Expression Conv_I1(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(int));
        public static Expression Conv_I2(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(int));
        public static Expression Conv_I4(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(int));
        public static Expression Conv_I8(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(Int64));
        public static Expression Conv_R4(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(float));
        public static Expression Conv_R8(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(double));

        public static Expression Conv_U(InstructionContext ctx)
        {
            var val = ctx.PopStack();

            if (val.Type == typeof(string))
                val = Expression.Call(CompilerHelpers.StringToPointerMethod, val);
            
            return Expression.Convert(val, typeof(UIntPtr));
        }

        public static Expression Conv_U1(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(int));
        public static Expression Conv_U2(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(int));
        public static Expression Conv_U4(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(int));
        public static Expression Conv_U8(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(Int64));

        public static Expression Conv_Ovf_I(InstructionContext ctx)
        {
            var val = ctx.PopStack();

            if (val.Type == typeof(string))
                val = Expression.Call(CompilerHelpers.StringToPointerMethod, val);
            
            return val;
        }

        public static Expression Conv_Ovf_I1(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_I2(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_I4(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_I8(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(Int64));

        public static Expression Conv_Ovf_U(InstructionContext ctx)
        {
            var val = ctx.PopStack();

            if (val.Type == typeof(string))
                val = Expression.Call(CompilerHelpers.StringToPointerMethod, val);
            
            return Expression.Convert(val, typeof(UIntPtr));
        }

        public static Expression Conv_Ovf_U1(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_U2(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_U4(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_U8(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(Int64));

        public static Expression Conv_R_Un(InstructionContext ctx) => Expression.Convert(ctx.PopStack(), typeof(float));

        public static Expression Conv_Ovf_I1_Un(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_I2_Un(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_I4_Un(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_I8_Un(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(Int64));

        public static Expression Conv_Ovf_I_Un(InstructionContext ctx)
        {
            var val = ctx.PopStack();

            if (val.Type == typeof(string))
                val = Expression.Call(CompilerHelpers.StringToPointerMethod, val);
            
            return val;
        }

        public static Expression Conv_Ovf_U1_Un(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_U2_Un(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_U4_Un(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(int));

        public static Expression Conv_Ovf_U8_Un(InstructionContext ctx) =>
            Expression.ConvertChecked(ctx.PopStack(), typeof(Int64));

        public static Expression Conv_Ovf_U_Un(InstructionContext ctx)
        {
            var val = ctx.PopStack();
            
            if (val.Type == typeof(string))
                val = Expression.Call(CompilerHelpers.StringToPointerMethod, val);
            var ptr = new IntPtr();
            
            var uIntPtr = new UIntPtr((uint)ptr.ToInt64());
            return Expression.Convert(val, typeof(UIntPtr));
        }

        public static Expression Ldlen(InstructionContext ctx)
        {
            var array = ctx.PopStack();
            return Expression.ArrayLength(array);
        }

        public static Expression Ldfld(InstructionContext ctx)
        {
            var fieldInfo = (FieldInfo)ctx.Instruction.Operand;
            var instance = ctx.PopStack();

            // TODO: this fails the NewMembers Test10 for some reason
            // if (fieldInfo is VirtualFieldInfo vfi) {
            //     var vfiConstant = Expression.Constant(vfi);
            //     var call = Expression.Call(vfiConstant, nameof(VirtualFieldInfo.GetValue), new Type[0], Expression.Constant(instance));
            //     return Expression.Convert(call, CompilerHelpers.ResolveVirtualType(vfi.FieldType));
            // }
            
            return CompilerHelpers.ResolveField(instance, fieldInfo);
        }

        public static Expression Ldflda(InstructionContext ctx)
        {
            var fieldInfo = (FieldInfo)ctx.Instruction.Operand;
            var instance = ctx.PopStack();
            return CompilerHelpers.ResolveField(instance, fieldInfo);
        }

        public static Expression Ldsfld(InstructionContext ctx)
        {
            var fieldInfo = (FieldInfo)ctx.Instruction.Operand;
            return CompilerHelpers.ResolveField(null, fieldInfo);
        }

        public static Expression Ldsflda(InstructionContext ctx)
        {
            var fieldInfo = (FieldInfo)ctx.Instruction.Operand;
            return CompilerHelpers.ResolveField(null, fieldInfo);
        }

        public static Expression Stsfld(InstructionContext ctx)
        {
            var fieldInfo = (FieldInfo)ctx.Instruction.Operand;
            var field = CompilerHelpers.ResolveField(null, fieldInfo);
            var value = CompilerHelpers.Coerce(ctx.PopStack(), field.Type);
            
            if (CompilerHelpers.RewriteAssignToNonWritableTargets(field, value, out var expr))
                return expr;
            
            return Expression.Assign(field, value);
        }

        public static Expression Stfld(InstructionContext ctx)
        {
            var fieldInfo = (FieldInfo)ctx.Instruction.Operand;
            var value = ctx.PopStack();
            var instance = ctx.PopStack();
            var field = CompilerHelpers.ResolveField(instance, fieldInfo);

            value = CompilerHelpers.Coerce(value, field.Type);

            if (CompilerHelpers.RewriteAssignToNonWritableTargets(field, value, out var expr))
                return expr;
            
            return Expression.Assign(field, value);
        }

        public static Expression Ldftn(InstructionContext ctx)
        {
            var methodInfo = (MethodInfo)ctx.Instruction.Operand;

            if (methodInfo is VirtualMethodInfo vmi) {
                ctx.ResultMetadata = vmi;
                return Expression.Constant(IntPtr.Zero);
            }
            
            return Expression.Constant(methodInfo.MethodHandle.GetFunctionPointer());
        }

        public static Expression Ldvirtftn(InstructionContext ctx)
        {
            var instance = ctx.PopStack();

            // We shouldn't resolve anything here, right?

            var methodInfo = (MethodInfo)ctx.Instruction.Operand;

            if (methodInfo.DeclaringType?.IsInterface == true) {
                return Expression.Call(CompilerHelpers.ResolveVirtualFunctionPointerMethod, instance,
                    Expression.Constant(methodInfo));
            }
            
            return Expression.Constant(methodInfo.MethodHandle.GetFunctionPointer());
        }

        public static Expression Pop(InstructionContext ctx)
        {
            // // Compiler might generate 
            // if (ctx.ValueStack.Count == 0)
            //     return Expression.Empty();
            return Expression.Block(typeof(void), ctx.PopStack());
        }

        public static Expression Box(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var boxType = (Type)ctx.Instruction.Operand;

            value = CompilerHelpers.Coerce(value, boxType);
            
            return Expression.Convert(value, typeof(object));
        }

        public static Expression Isinst(InstructionContext ctx)
        {
            var instance = ctx.PopStack();
            var type = (Type)ctx.Instruction.Operand;
            return Expression.Condition(Expression.TypeIs(instance, type), instance, Expression.Default(instance.Type));
        }

        public static Expression Unbox(InstructionContext ctx)
        {
            var instance = ctx.PopStack();
            var type = (Type)ctx.Instruction.Operand;
            return Expression.Convert(instance, type.ResolveVirtualType());
        }

        public static Expression Unbox_Any(InstructionContext ctx) => Unbox(ctx);

        public static Expression Rem(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();
            return Expression.Modulo(left, right);
        }

        public static Expression Rem_Un(InstructionContext ctx) => Rem(ctx);

        public static Expression And(InstructionContext ctx)
        {
            var right = CompilerHelpers.Coerce(ctx.PopStack(), typeof(bool));
            var left = CompilerHelpers.Coerce(ctx.PopStack(), typeof(bool));
            return Expression.And(left, right);
        }

        public static Expression Or(InstructionContext ctx)
        {
            var right = CompilerHelpers.Coerce(ctx.PopStack(), typeof(bool));
            var left = CompilerHelpers.Coerce(ctx.PopStack(), typeof(bool));
            return Expression.Or(left, right);
        }

        public static Expression Shl(InstructionContext ctx)
        {
            var amountOfBits = ctx.PopStack();
            var value = ctx.PopStack();
            return Expression.LeftShift(value, amountOfBits);
        }

        public static Expression Shr(InstructionContext ctx)
        {
            var amountOfBits = ctx.PopStack();
            var value = ctx.PopStack();
            return Expression.RightShift(value, amountOfBits);
        }

        public static Expression Shr_Un(InstructionContext ctx) => Shr(ctx);

        public static Expression Endfinally(InstructionContext ctx) => Expression.Empty();

        public static Expression Throw(InstructionContext ctx)
        {
            var exception = ctx.PopStack();
            return Expression.Throw(exception, ctx.Compiler.Method.ReturnType);
        }

        public static Expression Rethrow(InstructionContext ctx)
        {
            return Expression.Rethrow();
        }

        public static Expression Constrained(InstructionContext ctx)
        {
            return Expression.Empty();
        }

        public static Expression Endfilter(InstructionContext ctx) => ctx.PopStack();

        public static Expression Bgt(InstructionContext ctx)
        {
            var value2 = ctx.PopStack();
            var value1 = ctx.PopStack();
            var target = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index,((IlInstruction)ctx.Instruction.Operand).Index, true);
            return Expression.IfThen(Expression.GreaterThan(value1, value2), Expression.Goto(target));
        }

        public static Expression Bgt_S(InstructionContext ctx) => Bgt(ctx);
        public static Expression Bgt_Un(InstructionContext ctx) => Bgt(ctx);
        public static Expression Bgt_Un_S(InstructionContext ctx) => Bgt(ctx);

        public static Expression Bge(InstructionContext ctx)
        {
            var value2 = ctx.PopStack();
            var value1 = ctx.PopStack();
            var target = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index,((IlInstruction)ctx.Instruction.Operand).Index, true);
            return Expression.IfThen(Expression.Equal(value1, value2), Expression.Goto(target));
        }

        public static Expression Bge_S(InstructionContext ctx) => Bge(ctx);

        public static Expression Bge_Un(InstructionContext ctx) => Bge(ctx);

        public static Expression Bge_Un_S(InstructionContext ctx) => Bge(ctx);

        public static Expression Blt(InstructionContext ctx)
        {
            var value2 = ctx.PopStack();
            var value1 = ctx.PopStack();
            var target = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index,(int)((IlInstruction)ctx.Instruction.Operand).Index, true);
            return Expression.IfThen(Expression.LessThan(value1, value2), Expression.Goto(target));
        }

        public static Expression Blt_S(InstructionContext ctx) => Blt(ctx);
        public static Expression Blt_Un(InstructionContext ctx) => Blt(ctx);
        public static Expression Blt_Un_S(InstructionContext ctx) => Blt(ctx);

        public static Expression Castclass(InstructionContext ctx)
        {
            var type = (Type)ctx.Instruction.Operand;
            var instance = ctx.PopStack();
            return Expression.Convert(instance, type.ResolveVirtualType());
        }

        public static Expression Neg(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            return Expression.Negate(value);
        }

        public static Expression Mul(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();
            return Expression.Multiply(left, right);
        }

        public static Expression Div(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();
            return Expression.Divide(left, right);
        }

        public static Expression Div_Un(InstructionContext arg) => Div(arg);

        public static Expression Mul_Ovf(InstructionContext ctx)
        {
            var right = ctx.PopStack();
            var left = ctx.PopStack();
            return Expression.MultiplyChecked(left, right);
        }

        public static Expression Mul_Ovf_Un(InstructionContext ctx) => Mul_Ovf(ctx);

        public static Expression Switch(InstructionContext ctx)
        {
            var jumps = (IlInstruction[])ctx.Instruction.Operand;
            var value = ctx.PopStack();

            var switchCases = jumps.Select((j, index) =>
            {
                var instructionIndex = ctx.Instruction.Index;
                var labelTarget = ctx.Compiler.GetOrCreateLabelTarget(instructionIndex, j.Index, true);
                var gotoExpression = Expression.Goto(labelTarget);
                return Expression.SwitchCase(gotoExpression, CompilerHelpers.Coerce(Expression.Constant(index), value.Type));
            });
            
            return Expression.Switch(value, Expression.Empty(), switchCases.ToArray());
        }

        public static Expression Arglist(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ble(InstructionContext ctx)
        {
            var value2 = ctx.PopStack();
            var value1 = ctx.PopStack();
            var target = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index,((IlInstruction)ctx.Instruction.Operand).Index, true);
            return Expression.IfThen(Expression.LessThanOrEqual(value1, value2), Expression.Goto(target));
        }

        public static Expression Ble_S(InstructionContext arg) => Ble(arg);
        public static Expression Ble_Un(InstructionContext arg) => Ble(arg);
        public static Expression Ble_Un_S(InstructionContext arg) => Ble(arg);

        public static Expression Bne_Un(InstructionContext ctx)
        {
            var value2 = ctx.PopStack();
            var value1 = ctx.PopStack();
            var target = ctx.Compiler.GetOrCreateLabelTarget(ctx.Instruction.Index,((IlInstruction)ctx.Instruction.Operand).Index, true);

            CompilerHelpers.IntEnumCoerce(ref value1, ref value2);
            
            return Expression.IfThen(Expression.NotEqual(value1, value2), Expression.Goto(target));
        }
        public static Expression Bne_Un_S(InstructionContext arg) => Bne_Un(arg);

        public static Expression Break(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Calli(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Cpblk(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Cpobj(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Initblk(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Jmp(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldc_I8(InstructionContext ctx) => Expression.Constant(int.Parse((string)ctx.Instruction.Operand));

        public static Expression Ldind_I(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_I1(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_I2(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_I4(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_I8(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_R4(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_R8(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_Ref(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_U1(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_U2(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldind_U4(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Ldobj(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Localloc(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Mkrefany(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Not(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            return Expression.Not(value);
        }

        public static Expression Prefix1(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Prefix2(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Prefix3(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Prefix4(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Prefix5(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Prefix6(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Prefix7(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Prefixref(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Readonly(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Refanytype(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Refanyval(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Sizeof(InstructionContext arg)
        {
            throw new NotImplementedException();
        }

        public static Expression Starg(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var num = (int)ctx.Instruction.Operand;

            return Expression.Assign(ctx.Compiler.GetArgumentByIndex(num), value);
        }

        public static Expression Starg_S(InstructionContext arg) => Starg(arg);

        public static Expression Stind_I(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var target = ctx.PopStack();
            return Expression.Assign(target, value);
        }

        public static Expression Stind_I1(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var target = ctx.PopStack();
            return Expression.Assign(target, value);
        }

        public static Expression Stind_I2(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var target = ctx.PopStack();
            return Expression.Assign(target, value);
        }

        public static Expression Stind_I4(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var target = ctx.PopStack();
            return Expression.Assign(target, value);
        }

        public static Expression Stind_I8(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var target = ctx.PopStack();
            return Expression.Assign(target, value);
        }

        public static Expression Stind_R4(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var target = ctx.PopStack();
            return Expression.Assign(target, value);
        }

        public static Expression Stind_R8(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var target = ctx.PopStack();
            return Expression.Assign(target, value);
        }

        public static Expression Stind_Ref(InstructionContext ctx)
        {
            var value = ctx.PopStack();
            var target = ctx.PopStack();
            return Expression.Assign(target, value);
        }

        public static Expression Stobj(InstructionContext ctx)
        {
            throw new NotImplementedException();
        }

        public static Expression Tailcall(InstructionContext ctx)
        {
            throw new NotImplementedException();
        }

        public static Expression Unaligned(InstructionContext ctx)
        {
            throw new NotImplementedException();
        }

        public static Expression Volatile(InstructionContext ctx)
        {
            throw new NotImplementedException();
        }

        public static Expression Xor(InstructionContext ctx)
        {
            var value2 = ctx.PopStack();
            var value1 = ctx.PopStack();

            return Expression.ExclusiveOr(value2, value1);
        }

        public static Expression Ckfinite(InstructionContext arg)
        {
            throw new NotImplementedException();
        }
    }

    public class ByRefCall : Expression
    {
        public override Type Type => MethodInfo.ReturnType;
        public override ExpressionType NodeType => ExpressionType.Call;
        public Expression Instance { get; }
        public MethodInfo MethodInfo { get; }
        public Expression[] Arguments { get; }

        public override bool CanReduce => true;

        public override Expression Reduce()
        {
            return Call(Instance, MethodInfo, Arguments);
        }

        public ByRefCall(Expression thisConstant, MethodInfo mi, Expression[] arguments)
        {
            Instance = thisConstant;
            MethodInfo = mi;
            Arguments = arguments;
        }
    }
}