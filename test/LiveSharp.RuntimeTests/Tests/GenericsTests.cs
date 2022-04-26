namespace LiveSharp.RuntimeTests.Tests
{
    public class GenericsTests : TestsBase
    {
        public void Test0()
        {
            var result = GenericContainer.SameBody<int?>(2);
            
            AssertEqual(result, "2");
        }
        
        class GenericContainer
        {
            public static string SameBody<T>(T value)
            {
                return value.ToString();
            }
            
            public static string BodyUpdate<T>(T value)
            {
                return string.Empty;
            }
        }
    }
}