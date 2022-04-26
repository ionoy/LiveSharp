using LiveSharp.Runtime.IL;
using System;
using System.Linq;
using System.Reflection;

namespace LiveSharp.Runtime.Virtual
{
    public class GenericTypeResolver
    {
        private readonly Type[] _genericParameters;
        private readonly Type[] _genericArguments;
        
        public GenericTypeResolver(Type[] genericParameters, Type[] genericArguments)
        {
            _genericParameters = genericParameters;
            _genericArguments = genericArguments;
        }
        
        public object ApplyGenericArguments(object operand)
        {
            if (operand is GenericTypeParameter gtp)
                return ResolveGenericType(gtp);

            if (operand is VirtualMethodInfo vmi)
                return ApplyGenericArguments(vmi);

            if (operand is MethodInfo {ContainsGenericParameters: true} mi)
                return ApplyGenericArguments(mi);

            if (operand is FieldInfo fi)
                return ApplyGenericArguments(fi);

            if (operand is Type t)
                return ApplyGenericArguments(t);
            
            if (operand is GenericTypeResolverEval genericTypeResolverEval)
                return genericTypeResolverEval.Evaluate(this);

            return operand;
        }
        private MethodInfo ApplyGenericArguments(VirtualMethodInfo virtualMethodInfo)
        {
            var args = virtualMethodInfo.GenericArguments.ToArray();

            for (int i = 0; i < args.Length; i++)
                args[i] = ResolveGenericType(args[i]);

            var constructedMethod = virtualMethodInfo.MakeGenericMethod(null, args);

            return constructedMethod;
        }

        public object ApplyGenericArguments(Type type)
        {
            return ResolveGenericType(type);
        }

        MethodInfo ApplyGenericArguments(MethodInfo method)
        {
            // First, make sure that method itself doesn't contain any unresolved types
            if (method.ContainsGenericParameters && method.IsGenericMethod) {
                var args = method.GetGenericArguments().ToArray();
                
                for (int i = 0; i < args.Length; i++)
                    args[i] = ResolveGenericType(args[i]);

                var mDef = method.GetGenericMethodDefinition();
                method = mDef.MakeGenericMethod(args);
            } 
            
            // Now resolve the declaring type 
            var resolvedDeclaringType = ResolveGenericType(method.DeclaringType);
            if (method.DeclaringType != resolvedDeclaringType) {
                var candidates = resolvedDeclaringType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var methodParameterTypes = method.GetParameters().Select(p => ResolveGenericType(p.ParameterType)).ToArray();

                foreach (var c in candidates) {
                    var candidate = c;
                    if (candidate.Name != method.Name)
                        continue;

                    if (candidate.GetParameters().Length != method.GetParameters().Length)
                        continue;
                    
                    if (candidate.IsGenericMethod && method.IsGenericMethod) {
                        var lArgs = candidate.GetGenericArguments();
                        var rArgs = method.GetGenericArguments();
                        
                        if (lArgs.Length != rArgs.Length)
                            continue;
                        
                        // if (!candidate.IsGenericMethodDefinition && mi.IsGenericMethodDefinition) {
                        //     mi = mi.MakeGenericMethod(lArgs);
                        // } else 
                        if (candidate.IsGenericMethodDefinition && !method.IsGenericMethodDefinition) {
                            candidate = candidate.MakeGenericMethod(rArgs);
                        }
                    }

                    var candidateParameterTypes = candidate.GetParameters().Select(p => p.ParameterType);

                    if (candidateParameterTypes.SequenceEqual(methodParameterTypes)) {
                        method = candidate;
                        break;
                    }
                }
            }

            return method;
        }
        
        private object ApplyGenericArguments(FieldInfo fi)
        {
            var resolvedDeclaringType = ResolveGenericType(fi.DeclaringType);
            var resolvedField = resolvedDeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == fi.Name);

            return resolvedField;
        }
        
        public Type ResolveGenericType(Type type)
        {
            if (type is GenericTypeParameter gtp) {
                for (int i = 0; i < _genericParameters.Length; i++) {
                    if (_genericParameters[i] is GenericTypeParameter gtp2 && gtp.Name == gtp2.Name)
                        return _genericArguments[i];
                }

                throw new InvalidOperationException($"Couldn't find generic type argument for {gtp}");
            }
                
            if (type.IsGenericParameter) {
                for (int i = 0; i < _genericParameters.Length; i++) {
                    if (_genericParameters[i].GenericParameterPosition == type.GenericParameterPosition)
                        return _genericArguments[i];
                }

                throw new InvalidOperationException($"Couldn't find generic type argument for {type.Name}");
            } 
                
            if (type.ContainsGenericParameters) {
                var args = type.GetGenericArguments().ToArray();

                for (int i = 0; i < args.Length; i++)
                    args[i] = ResolveGenericType(args[i]);

                if (type is VirtualTypeInfo vti)
                    return vti.MakeGenericType(args);

                if (type is GenericTypeInstance typeInstance)
                    type = typeInstance.UnderlyingSystemType;
                
                return unwrapElementType(type, t => t.GetGenericTypeDefinition().MakeGenericType(args));

                Type unwrapElementType(Type t, Func<Type, Type> action)
                {
                    if (t.IsArray) {
                        var arrayRank = t.GetArrayRank();
                        return unwrapElementType(t.GetElementType(), action).MakeArrayType(arrayRank);
                    }
                    if (t.IsByRef)
                        return unwrapElementType(t.GetElementType(), action).MakeByRefType();
                    if (t.IsPointer)
                        return unwrapElementType(t.GetElementType(), action).MakePointerType();
                    if (t.HasElementType)
                        throw new NotSupportedException("Element type not supported: " + t);

                    return action(t);
                }
            }

            return type;
        }
    }
}