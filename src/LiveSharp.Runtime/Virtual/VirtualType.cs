using System.Reflection;
using System.Runtime.CompilerServices;

namespace LiveSharp.Runtime.Virtual
{
    public abstract class VirtualTypeBase : IReflectableType
    {
        public TypeInfo GetTypeInfo()
        {
            return LiveSharpRuntime.AssemblyContextRegistry.GetOrCreateVirtualTypeInfo(GetType());
        }
    }
    class VirtualType0 : VirtualTypeBase {}
    class VirtualType1 : VirtualTypeBase {}
    class VirtualType2 : VirtualTypeBase {}
    class VirtualType3 : VirtualTypeBase {}
    class VirtualType4 : VirtualTypeBase {}
    class VirtualType5 : VirtualTypeBase {}
    class VirtualType6 : VirtualTypeBase {}
    class VirtualType7 : VirtualTypeBase {}
    class VirtualType8 : VirtualTypeBase {}
    class VirtualType9 : VirtualTypeBase {}
    
    class VirtualAsyncStateMachine : IAsyncStateMachine
    {
        public int _moveNextToken;
        public void MoveNext()
        {
            if (_moveNextToken != 0) {
                var d = VirtualClr.ResolveDelegate(_moveNextToken);
                d.DynamicInvoke();
            }
        }

        public void SetStateMachine(IAsyncStateMachine stateMachine)
        {
            
        }
    }
}