namespace LiveSharp.RuntimeTests.Tests
{
    public class InheritanceTests : TestsBase
    {
        public override bool TestOverride()
        {
            Assert(base.TestOverride());
            return true;
        }
    }
}