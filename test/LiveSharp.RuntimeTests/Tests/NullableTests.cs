namespace LiveSharp.RuntimeTests.Tests
{
    public class NullableTests : TestsBase
    {
        public bool? NullableBool { get; set; }

        void Test0()
        {
            if (NullableBool == null) {
                Assert(true);
            } else {
                Assert(false);
            }
        }
        
        void Test1()
        {
            NullableBool = true;
            
            if (NullableBool == true) {
                Assert(true);
            } else {
                Assert(false);
            }
        }
        
        void Test2()
        {
            NullableBool = false;
            
            if (NullableBool == false) {
                Assert(true);
            } else {
                Assert(false);
            }
        }
    }
}