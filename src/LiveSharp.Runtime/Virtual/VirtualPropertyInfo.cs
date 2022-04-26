using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using LiveSharp.Runtime.Virtual;

namespace LiveSharp.Runtime
{
    public class VirtualPropertyInfo : PropertyInfo, IVirtualMemberInfo
    {
        // static readonly ConcurrentDictionary<Type, MethodInfo> ConstructedGetDefaultMethods = new ConcurrentDictionary<Type, MethodInfo>();
        public string FullName => _declaringType.FullName + "." + Name;
        public int Token { get; }
        public PropertyInfo StaticProperty { get; }
        
        private readonly string _name;
        private readonly VirtualTypeInfo _declaringType;
        private readonly Type _propertyType;
        private readonly VirtualMethodInfo _getMethod;
        private readonly VirtualMethodInfo _setMethod;
        // private readonly ConditionalWeakTable<object, object> _propertyValues;
        // private readonly object _lock = new object(); 
        private readonly object _nullObject = new object(); 
        
        public VirtualPropertyInfo(int token, string name, VirtualTypeInfo declaringType, Type returnType, PropertyInfo staticProperty, VirtualMethodInfo getter = null, VirtualMethodInfo setter = null)
        {
            _name = name;
            _declaringType = declaringType;
            _propertyType = returnType;
            
            var getterIdentifier = declaringType.FullName + " " + "get_" + name;
            var setterIdentifier = declaringType.FullName + " " + "set_" + name + " " + returnType.FullName;
            
            _getMethod = getter ?? declaringType.VirtualMethods.FirstOrDefault(m => m.MethodIdentifier == getterIdentifier);
            _setMethod = setter ?? declaringType.VirtualMethods.FirstOrDefault(m => m.MethodIdentifier == setterIdentifier);
            // _propertyValues = propertyValues;

            Token = token;
            StaticProperty = staticProperty;
        }

        public object this[object instance] 
        {
            get => GetValue(instance ?? _nullObject);
            set => SetValue(instance ?? _nullObject, value);
        }
        
        public new object GetValue(object instance) => GetValue(instance ?? _nullObject, BindingFlags.Default, null, null, CultureInfo.InvariantCulture);
        public new void SetValue(object instance, object value) => SetValue(instance ?? _nullObject, value,BindingFlags.Default, null, null, CultureInfo.InvariantCulture);
        
        public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            return _getMethod.Invoker.InvokeMethod<object>(obj ?? _nullObject, new object[0]);
        }

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            _setMethod.Invoker.InvokeMethodVoid(obj ?? _nullObject, new [] { value });
        }

        public override PropertyAttributes Attributes => PropertyAttributes.None;

        public override bool CanRead => true;

        public override bool CanWrite => true;

        public override Type PropertyType => _propertyType;

        public override Type DeclaringType => _declaringType;

        public override string Name => _name;

        public override Type ReflectedType => _propertyType;


        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }
        public override MethodInfo[] GetAccessors(bool nonPublic)
        {
            return new [] { _getMethod, _setMethod };
        }

        public override MethodInfo GetGetMethod(bool nonPublic)
        {
            return _getMethod;
        }

        public override MethodInfo GetSetMethod(bool nonPublic)
        {
            return _setMethod;
        }

        public override ParameterInfo[] GetIndexParameters()
        {
            return new ParameterInfo[0];
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return true;
        }
        
        public override string ToString()
        {
            return "v-property: " + _declaringType.FullName + "." + _name;
        }
    }
}