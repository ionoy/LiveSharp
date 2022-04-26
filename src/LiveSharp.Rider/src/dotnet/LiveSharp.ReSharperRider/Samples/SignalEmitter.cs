using JetBrains.DataFlow;
using JetBrains.Lifetimes;

namespace LiveSharp.RiderInterop
{
    public class SignalEmitter
    {
        public ISignal<string> SomethingHappened;

        public SignalEmitter(Lifetime lifetime)
        {
            SomethingHappened = new Signal<string>(lifetime, "SignalEmitter.SomethingHappened");
        }

        public void MakeItHappen(string arg)
        {
            SomethingHappened.Fire(arg);
        }
    }
}