using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Threading;

namespace LiveSharp.CSharp
{
    class LiveSharpAdditionalText : AdditionalText
    {
        public override string Path => _textDocument.FilePath;
            
        private readonly TextDocument _textDocument;
        public LiveSharpAdditionalText(TextDocument textDocument)
        {
            _textDocument = textDocument;
        }

        public override SourceText? GetText(CancellationToken cancellationToken = new CancellationToken())
        {
            if (_textDocument.TryGetText(out var text))
                return text;
            return null;
        }
    }
}