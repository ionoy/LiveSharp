using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Xml.Linq;
using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Virtual;

namespace LiveSharp.Runtime
{
    public class VirtualInvoker
    {
        private readonly IVirtualInvokable _parentInvokable;
        private readonly Func<object, object[], object> _defaultInvoker;

        public VirtualInvoker(IVirtualInvokable parentInvokable, Func<object, object[], object> defaultInvoker)
        {
            _parentInvokable = parentInvokable;
            _defaultInvoker = defaultInvoker;
        }
        
        public TReturn InvokeMethod<TReturn>(object instance, object[] arguments)
        {
            if (_parentInvokable.DelegateBuilder != null) {
                var executeResult = _parentInvokable.DelegateBuilder.Invoke(instance, arguments);

                return (TReturn)executeResult;
            }
            
            if (_defaultInvoker != null)
                return (TReturn)_defaultInvoker(instance, arguments);
            
            throw new Exception($"Can't invoke {_parentInvokable.DeclaringType}.{_parentInvokable.Name} because no default invoker or method body is defined");
        }
        
        public void InvokeMethodVoid(object instance, object[] arguments)
        {
            InvokeMethod<object>(instance, arguments);
        }

        public T InvokeConstructor<T>(object[] arguments) where T : new()
        {
            var constructorInfo = _parentInvokable as VirtualMethodInfo;
            
            if (constructorInfo == null)
                throw new InvalidOperationException("Cannot virtual invoke non-constructor object");
            
            var instance = (T)FormatterServices.GetUninitializedObject(constructorInfo.DeclaringType.UnderlyingSystemType);
            
            _parentInvokable.DelegateBuilder.Invoke(instance, arguments);
            
            return instance;
        }
    }
}