using System;
using Xamarin.Forms;
using Xamarin.Forms.Internals;

namespace LiveSharp.RuntimeTests
{
    public partial class TestRunner
    {
        internal class MockDeviceInfo : DeviceInfo
        {
            public override Size PixelScreenSize => throw new NotImplementedException();

            public override Size ScaledScreenSize => throw new NotImplementedException();

            public override double ScalingFactor => throw new NotImplementedException();
        }
    }
}