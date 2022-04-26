namespace LiveSharp.RuntimeTests.Tests
{
    public class DynamicTests : TestsBase
    {
        public void Test0()
        {
            var a = new []{1, 2};
            dynamic d = a;
            int b = d[0];
        }
    }
}