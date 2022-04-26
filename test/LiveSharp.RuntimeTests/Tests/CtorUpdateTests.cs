using System;
using LiveSharp.Runtime;

namespace LiveSharp.RuntimeTests
{
    public partial class CtorUpdateTests : TestsBase
    {
        class CtorDummyBase
        {
            public bool BaseCalled { get; set; }
            protected CtorDummyBase(bool result)
            {
                System.Console.WriteLine("from base: " + result);
                BaseCalled = result;
            }
        }
        
        class CtorDummy : CtorDummyBase
        {
            public bool GetOnlyProperty { get; }
            public bool CtorCalled = false;

            public CtorDummy() : base(true)
            {
                var methodIdentifier = LiveSharpRuntime.GetMethodIdentifier(typeof(CtorDummy), ".ctor", new Type[0]);
                
                if (LiveSharpRuntime.GetMethodUpdate(this.GetType().Assembly, methodIdentifier, out var virtualMethodInfo)) {
                    virtualMethodInfo.DelegateBuilder.Invoke(this);
                }
            }
        }
    }
}