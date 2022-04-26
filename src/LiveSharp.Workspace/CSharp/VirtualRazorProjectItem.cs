using Microsoft.AspNetCore.Razor.Language;
using System.IO;
using System.Text;

namespace LiveSharp
{
    public class VirtualRazorProjectItem : DefaultRazorProjectItem
    {
        private readonly string _sourceText;
        public VirtualRazorProjectItem(RazorProjectItem item, string sourceText) : base(item.BasePath, item.FilePath, item.RelativePhysicalPath, item.FileKind, null, null, item.CssScope)
        {
            _sourceText = sourceText;
        }
        
        public override Stream Read()
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(_sourceText));
        }
    }
}