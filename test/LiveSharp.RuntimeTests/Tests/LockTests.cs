namespace LiveSharp.RuntimeTests
{
    public class LockTests : TestsBase
    {
        public void Test0()
        {
            var b = false;

            lock (_lockObject)
                b = true;

            AssertEqual(b, true);
        }

        #region Helpers

        readonly object _lockObject = new object();

        #endregion
    }
}