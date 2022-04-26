using System.Collections.Generic;
using System.Text;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class StringParser : StreamingParser<string>
    {
        private readonly CompositeParser _compositeParser;

        public StringParser()
        {
            _compositeParser = CompositeParser
                .StartWith(new IntParser())
                .Next(intParser => new RawParser(intParser.GetIntValue()))
                .Build();
        }

        public override bool IsParsingComplete => _compositeParser.IsParsingComplete;
        
        public override void Serialize(string value, List<byte> result)
        {
            new ByteArrayParser().Serialize(Encoding.Unicode.GetBytes(value), result);
        }

        public override void Reset()
        {
            _compositeParser.Reset();
        }

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            return _compositeParser.Feed(buffer, bufferIndex);
        }

        public override object GetValue() => GetStringValue();

        public string GetStringValue()
        {
            var values = _compositeParser.GetValues();
            return Encoding.Unicode.GetString((byte[])values[values.Length - 1]); 
        } 
    }
}