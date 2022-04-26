using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiveSharp.Runtime.Virtual
{
    public class VirtualTypeInfo : TypeDelegator
    {
        public LiveSharpAssemblyContextRegistry LiveSharpAssemblyContextRegistry { get; }
        
        public Type UnderlyingType => _underlyingType;

        private readonly string _name;
        private readonly Type _underlyingType;

        public ConcurrentDictionary<string, VirtualPropertyInfo> VirtualProperties { get; } = new();
        public ConcurrentDictionary<string, VirtualFieldInfo> VirtualFields { get; } = new();
        public ConcurrentStack<VirtualMethodInfo> VirtualMethods { get; } = new();

        private Type[] _genericTypeArguments;

        public VirtualTypeInfo(string name, Type underlyingType, LiveSharpAssemblyContextRegistry assemblyContextRegistry)
        {
            _name = name;
            _underlyingType = underlyingType ?? throw new ArgumentNullException(nameof(underlyingType));
            
            LiveSharpAssemblyContextRegistry = assemblyContextRegistry ?? throw new ArgumentNullException(nameof(assemblyContextRegistry));
            typeImpl = underlyingType;
        }

        public override string FullName => _name;

        public override string Name {
            get {
                var split = _name.Split('.');
                return split.Length > 0 ? split[split.Length - 1] : _name;
            }
        }
        public VirtualPropertyInfo[] GetVirtualProperties() => VirtualProperties.Values.ToArray();
        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
        {
            var distinct = new Dictionary<string, PropertyInfo>();
            var staticProperties = _underlyingType.GetProperties(bindingAttr);
            var virtualProperties = GetVirtualProperties();

            foreach (var property in staticProperties)
                distinct[property.DeclaringType?.FullName + "." + property.Name] = property;
            
            foreach (var property in virtualProperties)
                distinct[property.DeclaringType?.FullName + "." + property.Name] = property;

            return distinct.Values.ToArray();
        }

        protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
        {   
            var staticProperty = _underlyingType.GetProperty(name, bindingAttr);
            if (staticProperty != null)
                return staticProperty;

            if (VirtualProperties.TryGetValue(name, out var virtualProperty))
                return virtualProperty;
            
            return null;
        }

        public override Type MakeArrayType() => UnderlyingType.MakeArrayType();

        public override Type MakeArrayType(int rank) => UnderlyingType.MakeArrayType(rank);

        public override Type MakePointerType() => UnderlyingType.MakePointerType();

        public override Type MakeByRefType() => UnderlyingType.MakeByRefType();
        
        public override Type[] GenericTypeArguments => UnderlyingType != null ? UnderlyingType.GetGenericArguments() : _genericTypeArguments;

        public override Type MakeGenericType(params Type[] typeArguments)
        {
            //_genericTypeArguments = typeArguments;
            return new VirtualTypeInfo(Name, _underlyingType.MakeGenericType(typeArguments), LiveSharpAssemblyContextRegistry) {
                _genericTypeArguments = typeArguments
            };
        }

        public override Type[] GetGenericArguments() => GenericTypeArguments;
        
        public override bool IsGenericType => UnderlyingType.IsGenericType;
        public override bool IsGenericTypeDefinition => UnderlyingType.IsGenericTypeDefinition;
        public override bool IsGenericParameter => UnderlyingType.IsGenericParameter;
        public override bool ContainsGenericParameters => UnderlyingType.ContainsGenericParameters;
        public override bool IsConstructedGenericType => _underlyingType.IsConstructedGenericType;
    }

}