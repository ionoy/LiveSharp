using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Virtual;
using System;

namespace LiveSharp.Runtime
{
    public interface ILiveSharpRuntimeExt
    {
        bool IsDynamicMethodSupported();
        Delegate GetDelegate(DelegateBuilder delegateBuilder, VirtualMethodBody methodBody, IlInstructionList instructions, Type delegateType, ILogger logger, out object compiler);
    }
}