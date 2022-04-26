using LiveBlazor.Dashboard.Razor;
using NUnit.Framework;

namespace LiveBlazor.Tests
{
    public class TextEditTests
    {

        [Test]
        public void Test1()
        {
            var input = "abc def ghi";
            var edit1 = new NormalTextEdit(new TextSpan(4, 4), "def");
            var edit2 = new NormalTextEdit(new TextSpan(4, 7), "");
            var compositeTextEdit = new CompositeTextEdit(edit1, edit2);

            var result = compositeTextEdit.Apply(input);
            
            Assert.AreEqual(result, "abc def ghi");
        }
    }
}