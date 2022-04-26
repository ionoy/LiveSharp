namespace LiveSharp.RuntimeTests.Tests
{
    public class StructTests : TestsBase
    {
        public void Test0()
        {
            var arrayTest = new[] {new MyTest()};
            arrayTest[0].a = 3;
            
            AssertEqual(arrayTest[0].a, 3);
        }
        
        struct MyTest {
            public int a;
        };
    }
}