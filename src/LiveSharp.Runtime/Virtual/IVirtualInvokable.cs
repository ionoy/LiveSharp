using System;
using LiveSharp.Runtime.IL;

namespace LiveSharp.Runtime
{
    public interface IVirtualInvokable
    {
        Type DeclaringType { get; }
        string Name { get; }
        Type[] GetParameterTypes();
        
        DelegateBuilder DelegateBuilder { get; }
    }
}