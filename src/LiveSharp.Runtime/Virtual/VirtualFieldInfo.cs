using LiveSharp.Runtime.Virtual;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace LiveSharp.Runtime
{
    public class VirtualFieldInfo : FieldInfo, IVirtualMemberInfo
    {
        public int Token { get; }
        public FieldInfo CompiledField { get; }
        public string FullName => _declaringType.FullName + "." + Name;
        
        private readonly string _name;
        private readonly VirtualTypeInfo _declaringType;
        private readonly Type _fieldType;
        private readonly FieldAttributes _fieldAttributes;
        private readonly ConditionalWeakTable<object, object> _fieldValues;
        private readonly LiveSharpAssemblyContext _liveSharpAssemblyContext;
        private readonly ConcurrentDictionary<Type, VirtualFieldInfo> _constructedFieldCache = new();
        private readonly object _lock = new(); 
        private readonly object _nullObject = new();
        private byte[] _initialValue;

        public VirtualFieldInfo(int token, string name, VirtualTypeInfo declaringType, Type fieldType, FieldAttributes fieldAttributes, ConditionalWeakTable<object, object> fieldValues, FieldInfo compiledField, LiveSharpAssemblyContext liveSharpAssemblyContext)
        {
            _name = name;
            _declaringType = declaringType;
            _fieldType = fieldType;
            _fieldAttributes = fieldAttributes;
            _fieldValues = fieldValues;
            _liveSharpAssemblyContext = liveSharpAssemblyContext;
            Token = token;
            CompiledField = compiledField;
        }
        
        public object this[object instance] 
        {
            get => GetValue(instance ?? _nullObject);
            set => SetValue(instance ?? _nullObject, value);
        }
        
        public new void SetValue(object instance, object value) => SetValue(instance ?? _nullObject, value,BindingFlags.Default, null, CultureInfo.InvariantCulture);

        public ref TValue GetValueRef<TValue>(object obj)
        {
            lock (_lock) {
                var key = obj ?? _nullObject;
                if (_fieldValues.TryGetValue(key, out var val)) {
                    if (FieldType.IsValueType) {
                        var valueArray = (TValue[])val;
                        return ref valueArray[0];
                    }
                } else {
                    _fieldValues.Add(key, new TValue[1]);
                    return ref GetValueRef<TValue>(obj);
                }
            }

            throw new NotSupportedException($"GetValueRef not supported for {FieldType}");
        } 

        public override object GetValue(object obj)
        {
            if (CompiledField != null)
                return CompiledField.GetValue(obj);

            lock (_lock) {
                if (_fieldValues.TryGetValue(obj ?? _nullObject, out var val)) {
                    if (FieldType.IsValueType) {
                        var valueArray = (Array)val;
                        return valueArray.GetValue(0);
                    }
                    return val;
                }
            }

            if (_initialValue != null)
                return _initialValue;

            if (_fieldType.IsValueType)
                return Activator.CreateInstance(_fieldType);

            return null;
        }

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            if (CompiledField != null)
            {
                CompiledField.SetValue(obj, value);
            }
            else
            {
                lock(_lock) {
                    _fieldValues.Remove(obj);

                    if (FieldType.IsValueType) {
                        var arr = Array.CreateInstance(FieldType, 1);
                        arr.SetValue(value, 0);
                        _fieldValues.Add(obj, arr);
                    } else {
                        _fieldValues.Add(obj, value);
                    }
                }
            }
        }

        public override FieldAttributes Attributes => _fieldAttributes;

        public override RuntimeFieldHandle FieldHandle => new RuntimeFieldHandle();

        public override Type FieldType => _fieldType;

        public override Type DeclaringType => _declaringType;

        public override string Name => _name;

        public override Type ReflectedType => _fieldType;
        public FieldInfo BackingField { get; set; }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return new object[0];
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return new object[0];
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return false;
        }
        
        public override string ToString()
        {
            return "v-field: " + _declaringType.FullName + "." + _name;
        }

        public void SetInitialValue(byte[] initialValue)
        {
            _initialValue = initialValue;
        }
        
        public VirtualFieldInfo MakeGenericField(Type fieldType)
        {
            if (_constructedFieldCache.TryGetValue(fieldType, out var constructedField))
                return constructedField;

            constructedField = _liveSharpAssemblyContext.CreateVirtualFieldInfo(_name, _declaringType, fieldType, _fieldAttributes, _fieldValues, CompiledField);
            _constructedFieldCache[fieldType] = constructedField;
            return constructedField;
        }
    }
}