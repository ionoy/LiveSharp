using JetBrains.DataFlow;
using JetBrains.Lifetimes;
using JetBrains.Util;

namespace LiveSharp.RiderInterop
{
    public class SignalListener
    {
        public SignalListener(Lifetime lifetime, SignalEmitter signalEmitter)
        {
            signalEmitter.SomethingHappened.Advise(lifetime,
                arg => MessageBox.ShowInfo($"{arg}"));            
        }
    }
}