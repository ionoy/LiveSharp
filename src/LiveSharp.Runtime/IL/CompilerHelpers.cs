using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using LiveSharp.Runtime.Virtual;

namespace LiveSharp.Runtime.IL
{
    public class CompilerHelpers
    {
        private static readonly MethodInfo _fieldInfoSetValueMethod = typeof(FieldInfo).GetMethod(nameof(FieldInfo.SetValue), new[] { typeof(object), typeof(object) });
        public static readonly MethodInfo InitializeArrayMethod = typeof(CompilerHelpers).GetMethod(nameof(InitializeArray), new[] { typeof(Array), typeof(VirtualFieldInfo) });
        public static readonly MethodInfo ResolveVirtualFunctionPointerMethod = typeof(CompilerHelpers).GetMethod(nameof(ResolveVirtualFunctionPointer), new[] { typeof(object), typeof(MethodInfo) });
        public static readonly MethodInfo StringToPointerMethod = typeof(CompilerHelpers).GetMethod(nameof(StringToPointer), new[] { typeof(string) });
        
        public static Expression AssignCoerce(Expression left, Expression right)
        {
            if (right is StackSlotExpression stackSlot)
                stackSlot.SpeculativeTargetType = left.Type;
            
            return Expression.Assign(left, Coerce(right, left.Type));
        }

        public static Expression CoerceIntToBool(Expression expr)
        {
            if (!IsArithmetic(expr.Type))
                throw new InvalidOperationException($"Expected 'expr' type 'int', got '{expr.Type}");
                
            return Expression.NotEqual(expr, Coerce(Expression.Constant(0), expr.Type));
        }

        public static void CoerceArguments(Expression[] arguments, ParameterInfo[] parameters)
        {
            for (int i = 0; i < parameters.Length; i++) {
                var argument = arguments[i];
                var parameter = parameters[i];
                var parameterType = parameter.ParameterType;
                
                arguments[i] = Coerce(argument, parameterType);
            }
        }

        public static Expression Coerce(Expression expr, Type targetType)
        {
            if (expr.Type == targetType)
                return expr;

            if (IsSameType(expr.Type, targetType))
                return expr;

            // if (expr is StackSlotExpression stackSlot) {
            //     stackSlot.ChangeType(targetType);
            //     return stackSlot;
            // }

            if (targetType.IsByRef && !expr.Type.IsByRef) {
                var elementType = targetType.GetElementType();
                return Coerce(expr, elementType);
            }

            if (IsArithmetic(expr.Type) && targetType == typeof(bool))
                return CoerceIntToBool(expr);

            if (expr.Type == typeof(bool) && IsArithmetic(targetType))
                return CoerceBoolToInt(expr, targetType);

            if (!expr.Type.IsValueType && targetType == typeof(bool))
                return Expression.NotEqual(expr, Expression.Constant(null));

            if (expr.Type.IsEnum && targetType == typeof(bool))
                return Expression.NotEqual(Expression.Convert(expr, typeof(int)), Expression.Constant(0));
                
            if (!expr.Type.IsValueType && IsArithmetic(targetType))
                return Expression.Condition(Expression.NotEqual(expr, Expression.Constant(null)), Expression.Constant(1), Expression.Constant(0));
            
            if (targetType is VirtualTypeInfo vti)
                return Expression.Convert(expr, vti.UnderlyingType);
            
            // null?
            return Expression.Convert(expr, targetType);
        }

        public static void IntEnumCoerce(ref Expression left, ref Expression right)
        {
            if (left.Type == typeof(int) && right.Type.IsEnum) {
                right = Coerce(right, left.Type);
            } else if (right.Type == typeof(int) && left.Type.IsEnum) {
                left = Coerce(left, right.Type);
            }
        }

        private static Expression CoerceBoolToInt(Expression expr, Type targetType)
        {
            return Expression.Convert(expr, targetType);
        }

        public static bool IsArithmetic(Type type)
        {
            type = GetNonNullableType(type);
            if (!type.IsEnum)
            {
                var typeCode = Type.GetTypeCode(type);
                if ((uint)(typeCode - 7) <= 7u)
                    return true;
            }
            return false;
        }
        
        public static Type GetNonNullableType(Type type)
        {
            if (!IsNullableType(type))
                return type;
            return type.GetGenericArguments()[0];
        }
        
        public static bool IsNullableType(Type type)
        {
            if (type.IsConstructedGenericType)
            {
                return type.GetGenericTypeDefinition() == typeof(Nullable<>);
            }
            return false;
        }

        public static (Expression, Expression) EnsureArithmeticBinary(Expression left, Expression right)
        {
            left = EnsureArithmetic(left);
            right = EnsureArithmetic(right);
            
            return (left, right);
        }

        public static Expression EnsureArithmetic(Expression expr)
        {
            if (IsArithmetic(expr.Type))
                return expr;
            
            if (expr.Type == typeof(bool))
                return Expression.Convert(expr, typeof(int));
            
            if (expr.Type.IsEnum)
                return Expression.Convert(expr, expr.Type.GetEnumUnderlyingType());

            return Expression.Convert(expr, typeof(int));
        }
        
        public static Expression[] GetArguments(InstructionContext context, ParameterInfo[] parameters)
        {
            var arguments =  parameters.Select(p => context.PopStack()).Reverse().ToArray();

            for (int i = 0; i < arguments.Length; i++) {
                var argument = arguments[i];
                var parameter = parameters[i];

                if (argument is StackSlotExpression stackSlot)
                    stackSlot.SpeculativeTargetType = parameter.ParameterType;
            }
            
            return arguments;
        }

        public static bool IsSameType(Type left, Type right)
        {
            if (left == right)
                return true;
            if (left is VirtualTypeInfo && left.UnderlyingSystemType == right)
                return true;
            if (right is VirtualTypeInfo && right.UnderlyingSystemType == left)
                return true;
            return false;
        }

        public static Expression ResolveField(Expression instance, FieldInfo fieldInfo)
        {
            if (fieldInfo is VirtualFieldInfo vfi)
                return GetVirtualFieldAccess(vfi, instance);
            return Expression.Field(instance, fieldInfo);
        }

        public static Type ResolveVirtualType(Type type)
        {
            if (type is VirtualTypeInfo virtualTypeInfo)
                return virtualTypeInfo.UnderlyingSystemType;

            return type;
        }
        
        public static Expression GetVirtualFieldAccess(VirtualFieldInfo vfi, Expression instance)
        {
            var accessor = vfi.FieldType is VirtualTypeInfo
                ? Expression.New(typeof(VirtualMemberAccessor<>).MakeGenericType(typeof(object)))
                : Expression.New(typeof(VirtualMemberAccessor<>).MakeGenericType(vfi.FieldType));
            return Expression.Property(accessor, "Item", Expression.Convert(instance ?? Expression.Constant(null), typeof(object)), Expression.Constant(vfi));
        }
        
        public static bool RewriteAssignToNonWritableTargets(Expression member, Expression value, out Expression expr)
        {
            // readonly fields should be substituted by SetValue(field)
            if (member is MemberExpression fieldExpr && fieldExpr.Member is FieldInfo field && field.IsInitOnly) {
                if (field.IsStatic) {
                    // static initonly fields shouldn't be set after initialization
                    // https://github.com/dotnet/runtime/issues/11571
                    // I assume that the field was already initialized if we have code referencing it
                    // update: not fucking necessarily!
                    // we still need to check if it has been initialized already and if not, then initialize it
                    // otherwise we might overwrite the initializer before it has been run
                    // which leads to no initialization at all
                    expr = Expression.IfThen(Expression.Equal(member, Expression.Default(member.Type)),
                        CreateSetValueCall(field, fieldExpr.Expression, value));
                    return true;
                }
                
                expr = CreateSetValueCall(field, fieldExpr.Expression, value);
                return true;
            }

            expr = null;
            return false;
        }
            
        public static Expression CreateSetValueCall(FieldInfo backingField, Expression target, Expression value)
        {
            var targetExpression = target == null ? (Expression)Expression.Constant(null) : Expression.Convert(target, typeof(object));
            return Expression.Call(Expression.Constant(backingField),
                _fieldInfoSetValueMethod,
                targetExpression,
                Expression.Convert(value, typeof(object)));
        }
        
        public static Expression CreateCallVirtualMethodExpression(Expression instance, VirtualMethodInfo virtualMethodInfo, Expression[] arguments)
        {
            var args = !virtualMethodInfo.IsStatic 
                ? new[] {instance}.Concat(arguments) 
                : arguments;

            var metadata = virtualMethodInfo.DelegateBuilder;
            var metadataConstant = Expression.Constant(metadata);
            var delegateField = Expression.Field(metadataConstant, DelegateBuilder.DelegateFieldName);
            var delegateType = metadata.DelegateType;
            
            var delegateFieldConverted = Expression.Convert(delegateField, delegateType);
            
            return Expression.Invoke(delegateFieldConverted, args);
        }

        public static void InitializeArray(Array array, VirtualFieldInfo virtualFieldInfo)
        {
            var value = virtualFieldInfo.GetValue(null);
            if (value is byte[] byteArray) 
                Buffer.BlockCopy(byteArray, 0, array, 0, byteArray.Length);
        }

        public static IntPtr ResolveVirtualFunctionPointer(object instance, MethodInfo interfaceMethodInfo)
        {
            var type = instance.GetType();
            
            while (type != null) {
                var publicMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);

                foreach (var publicMethod in publicMethods) {
                    if (publicMethod.Name == interfaceMethodInfo.Name) {
                        var publicMethodParameters = publicMethod.GetParameters();
                        var interfaceMethodParameters = interfaceMethodInfo.GetParameters();
                        
                        if (publicMethodParameters.Length != interfaceMethodParameters.Length)
                            continue;

                        var parametersDifferent = false;
                        
                        for (int i = 0; i < publicMethodParameters.Length; i++) {
                            var publicParm = publicMethodParameters[i];
                            var interfaceParm = interfaceMethodParameters[i];

                            if (publicParm.ParameterType != interfaceParm.ParameterType) {
                                parametersDifferent = true;
                                break;
                            }
                        }

                        if (!parametersDifferent) {
                            return publicMethod.MethodHandle.GetFunctionPointer();
                        }
                    }
                }
                
                type = type.BaseType;
            }
            
            throw new InvalidOperationException($"Couldn't resolve virtual method {interfaceMethodInfo} on instance {instance}");
        }

        public static IntPtr StringToPointer(string obj)
        {
            return Marshal.StringToHGlobalAuto(obj);
        }
    }
}