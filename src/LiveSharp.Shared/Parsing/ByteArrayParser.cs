using System.Collections.Generic;
using System.Linq;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    class ByteArrayParser : StreamingParser<byte[]>
    {
        private readonly CompositeParser _compositeParser;

        public ByteArrayParser()
        {
            _compositeParser = CompositeParser.StartWith(new IntParser())
                .Next(intParser => new RawParser(intParser.GetIntValue()))
                .Build();
        }
        
        public override void Serialize(byte[] b, List<byte> result)
        {
            if (b != null) {
                new IntParser().Serialize(b.Length, result);
                new RawParser(b.Length).Serialize(b, result);
            } else {
                new IntParser().Serialize(-1, result);
            }
        }

        public override bool IsParsingComplete => _compositeParser.IsParsingComplete;

        public override void Reset() => _compositeParser.Reset();

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            return _compositeParser.Feed(buffer, bufferIndex);
        }

        public override object GetValue() => GetByteArrayValue();

        public IReadOnlyList<byte> GetByteArrayValue() => (IReadOnlyList<byte>)_compositeParser.GetValues().Last();
    }
}