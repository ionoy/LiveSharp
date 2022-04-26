using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using LiveSharp.Runtime.IL;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveSharp.Runtime.Virtual
{
    [DebuggerDisplay("v-method: {MethodIdentifier}")]
    public class VirtualMethodInfo : MethodInfo, IVirtualMemberInfo, IVirtualInvokable
    {
        public string MethodIdentifier { get; }
        public int Token { get; }
        public MethodInfo CompiledMethod { get; }
        public int MaxStackSize { get; }
        public bool IsGeneric { get; }

        public override bool IsDefined(Type attributeType, bool inherit) => true;
        public override Type DeclaringType => _declaringType;

        public DelegateBuilder DelegateBuilder => _methodMetadata.Value;
        public VirtualInvoker Invoker { get; }
        public ParameterMetadata[] Parameters { get; }
        public Type DelegateTypeOverride { get; set; }
        public LiveSharpAssemblyContext AssemblyContext { get; }

        private readonly ConcurrentDictionary<Type[], VirtualMethodInfo> _constructedGenericMethodsNoDelegateType = new ();
        private readonly ConcurrentDictionary<Type, VirtualMethodInfo> _constructedMethodCache = new();
        private readonly Lazy<DelegateBuilder> _methodMetadata;
        private readonly DocumentMetadata _documentMetadata;

        private readonly XElement _element;
        private readonly VirtualTypeInfo _declaringType;
        private readonly string _name;
        private readonly Type _returnType;
        private readonly bool _isStatic;

        public Type[] GenericParameters { get; } = new Type[0];
        public List<Type> GenericArguments { get; set; } = new();

        private Lazy<VirtualMethodBody> _methodBodyLoader;

        public VirtualMethodInfo(DocumentMetadata documentMetadata,
            LiveSharpAssemblyContext assemblyContext,
            XElement element,
            MethodInfo compiledMethod,
            string name,
            string methodIdentifier,
            VirtualTypeInfo virtualDeclaringType,
            Type returnType,
            bool isStatic,
            int maxStackSize,
            bool isGeneric,
            Type[] genericParameters,
            ParameterMetadata[] parameters,
            Func<VirtualMethodBody> bodyLoader,
            Func<object, object[], object> runtimeInvoker = null)
        {

            MethodIdentifier = methodIdentifier;
            Token = assemblyContext.CreateMetadataToken(this);
            CompiledMethod = compiledMethod;

            _documentMetadata = documentMetadata;
            AssemblyContext = assemblyContext;
            _element = element;
            _methodMetadata = new Lazy<DelegateBuilder>(CreateMethodMetadata);
            _methodBodyLoader = new Lazy<VirtualMethodBody>(bodyLoader);
            
            virtualDeclaringType.VirtualMethods.Push(this);

            _declaringType = virtualDeclaringType;
            _name = name;
            _returnType = returnType;
            _isStatic = isStatic;

            MaxStackSize = maxStackSize;
            IsGeneric = isGeneric;

            GenericParameters = genericParameters;

            Parameters = parameters;
            Invoker = new VirtualInvoker(this, runtimeInvoker);
        }

        private DelegateBuilder CreateMethodMetadata()
        {
            var delegateType = DelegateTypeOverride ?? AssemblyContext.GetDelegateType(this);
            return new(_element, AssemblyContext, this, delegateType, AssemblyContext.Registry.Logger);
        }

        public override object Invoke(object instance, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
        {
            return Invoker.InvokeMethod<object>(instance, parameters);
        }

        public Type[] GetParameterTypes() => Parameters.Select(p => p.ParameterType).ToArray();
        public new VirtualMethodBody GetMethodBody()
        {
            return _methodBodyLoader.Value;
        }

        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotImplementedException();

        public override MethodAttributes Attributes {
            get {
                var virtualFlags = (_isStatic ? MethodAttributes.Static : 0) | MethodAttributes.Public;
                if (CompiledMethod != null)
                    return CompiledMethod.Attributes | virtualFlags;
                return virtualFlags;
            }
        }

        public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();

        public override string Name => _name;

        public override Type ReflectedType => throw new NotImplementedException();
        public override Type ReturnType => _returnType;
        public override bool ContainsGenericParameters => GenericParameters.Length > 0;
        public override Type[] GetGenericArguments() => GenericParameters;

        public override MethodInfo GetBaseDefinition()
        {
            return null;
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return new object[0];
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return new object[0];
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            return MethodImplAttributes.Managed;
        }

        public override ParameterInfo[] GetParameters()
        {
            return GetParameterTypes().Select((pt, i) => new VirtualParameterInfo(pt, "arg" + 1)).OfType<ParameterInfo>().ToArray();
        }

        public override string ToString()
        {
            return "v-method: " + MethodIdentifier;
        }

        public VirtualMethodInfo MakeGenericMethod<TDelegate>() where TDelegate : class
        {
            var delegateType = typeof(TDelegate);
            return MakeGenericMethod(delegateType);
        }

        public VirtualMethodInfo MakeGenericMethod(Type delegateType = null, Type[] genericArguments = null)
        {
            if (delegateType != null && _constructedMethodCache.TryGetValue(delegateType, out var d))
                return d;

            if (delegateType == null && genericArguments != null) {
                foreach (var kvp in _constructedGenericMethodsNoDelegateType)
                    if (kvp.Key.SequenceEqual(genericArguments))
                        return kvp.Value;
            }

            if (genericArguments == null)
                genericArguments = delegateType == null 
                    ? GenericArguments.ToArray()
                    : delegateType.GetGenericArguments();
            
            var newParameters = Parameters.ToArray();
            var resolver = new GenericTypeResolver(GenericParameters, genericArguments);

            for (int i = 0; i < newParameters.Length; i++) {
                var parameter = newParameters[i];
                var resolvedType = resolver.ResolveGenericType(parameter.ParameterType);
                newParameters[i] = new ParameterMetadata(parameter.ParameterName, resolvedType);
            }

            var newBody = GetMethodBody().Clone();

            foreach (var local in newBody.Locals) {
                local.LocalType = resolver.ResolveGenericType(local.LocalType);
            }

            foreach (var instruction in newBody.Instructions) {
                instruction.Operand = resolver.ApplyGenericArguments(instruction.Operand);
            }

            var declaringType = resolver.ResolveGenericType(_declaringType);
            var returnType = resolver.ResolveGenericType(_returnType);

            var constructedMethod = new VirtualMethodInfo(
                _documentMetadata,
                AssemblyContext,
                _element,
                CompiledMethod,
                _name,
                MethodIdentifier,
                (VirtualTypeInfo)declaringType,
                returnType,
                _isStatic,
                MaxStackSize,
                IsGeneric,
                new Type[0],
                newParameters,
                () => newBody);
            
            constructedMethod.GenericArguments.AddRange(genericArguments);

            if (delegateType != null) {
                constructedMethod.DelegateTypeOverride = delegateType;
                _constructedMethodCache[constructedMethod.DelegateBuilder.DelegateType] = constructedMethod;
            } else {
                _constructedGenericMethodsNoDelegateType[genericArguments] = constructedMethod;
            }

            return constructedMethod;
        }
    }
}