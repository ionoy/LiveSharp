using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Virtual;
using LiveSharp.ServerClient;

namespace LiveSharp.Runtime
{
    // public class VirtualConstructorInfo : ConstructorInfo, IVirtualMemberInfo, IVirtualInvokable
    // {
    //     private readonly VirtualTypeInfo _declaringType;
    //     private readonly Type[] _parameterTypes;
    //     public MethodMetadata Metadata => _declaringType.VirtualAssembly.AllMethods[MethodIdentifier];
    //     public override MethodAttributes Attributes { get; }
    //     public override RuntimeMethodHandle MethodHandle { get; }
    //     public VirtualInvoker Invoker { get; }
    //
    //     public VirtualConstructorInfo(string name, string methodIdentifier, VirtualTypeInfo declaringType,
    //         Type[] parameterTypes, ConstructorInfo compiledConstructor, int[] genericParameterTokens)
    //     {
    //         Name = name;
    //         MethodIdentifier = methodIdentifier;
    //         CompiledConstructor = compiledConstructor;
    //         GenericParameterTokens = genericParameterTokens;
    //
    //         _declaringType = declaringType;
    //         _parameterTypes = parameterTypes;
    //         
    //         Invoker = new VirtualInvoker(this, null);
    //     }
    //     
    //     public override object[] GetCustomAttributes(bool inherit)
    //     {
    //         return new object[0];
    //     }
    //
    //     public override object[] GetCustomAttributes(Type attributeType, bool inherit)
    //     {
    //         return new object[0];
    //     }
    //
    //     public override bool IsDefined(Type attributeType, bool inherit) => false;
    //
    //     public override Type DeclaringType  => _declaringType.UnderlyingType;
    //     public override string Name { get; }
    //     public string MethodIdentifier { get; }
    //     public ConstructorInfo CompiledConstructor { get; }
    //     public int[] GenericParameterTokens { get; }
    //
    //     public override Type ReflectedType => _declaringType;
    //     
    //     public override MethodImplAttributes GetMethodImplementationFlags()
    //     {
    //         return MethodImplAttributes.Managed;
    //     }
    //
    //     public Type[] GetParameterTypes() => _parameterTypes;
    //     
    //     public override ParameterInfo[] GetParameters()
    //     {
    //         return GetParameterTypes().Select((pt, i) => new VirtualParameterInfo(pt, "arg" + 1))
    //                                   .OfType<ParameterInfo>()
    //                                   .ToArray();
    //     }
    //     
    //     public override object Invoke(BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
    //     {
    //         var instance = _declaringType.Assembly.CreateInstance(_declaringType.FullName);
    //         return Invoke(instance, invokeAttr, binder, parameters, culture);
    //     }
    //
    //     public override object Invoke(object instance, BindingFlags invokeAttr, Binder binder, object[] arguments, CultureInfo culture)
    //     {
    //         return Metadata.Invoke(instance, arguments);
    //     }
    //
    //     public override string ToString()
    //     {
    //         return _declaringType + " " + Name;
    //     }
    // }
}