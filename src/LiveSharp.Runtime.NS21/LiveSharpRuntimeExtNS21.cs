using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Virtual;
using System;
using System.Reflection.Emit;

namespace LiveSharp.Runtime.NS21
{
    public class LiveSharpRuntimeExtNS21 : ILiveSharpRuntimeExt
    {
        private readonly Lazy<bool> _isDynamicMethodSupported = new (IsDynamicMethodSupportedImpl);
        

        public bool IsDynamicMethodSupported()
        {
            return _isDynamicMethodSupported.Value;
        }

        public Delegate GetDelegate(DelegateBuilder delegateBuilder, VirtualMethodBody methodBody, IlInstructionList instructions, Type delegateType, ILogger logger, out object compiler)
        {
            var dmCompiler = new IlDynamicMethodCompiler(delegateBuilder, methodBody, instructions, logger);
            compiler = dmCompiler;
            return dmCompiler.GetDelegate(delegateType);
        }

        private static bool IsDynamicMethodSupportedImpl()
        {
            try {
                var dm = new DynamicMethod("compat_check", typeof(void), new Type[0]);
                var ilInfo = dm.GetDynamicILInfo();
                ilInfo.SetCode(new byte[0], 0);
                ilInfo.SetExceptions(new byte[0]);
                ilInfo.SetLocalSignature(new byte[0]);
                return true;
            }
            catch {
                return false;
            }
        }
    }
}