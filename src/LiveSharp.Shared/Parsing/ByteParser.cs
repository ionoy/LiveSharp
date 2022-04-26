using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class ByteParser : StreamingParser<byte>
    {
        public byte Value { get; private set; }
        public override bool IsParsingComplete => _isValueRead;
        
        private bool _isValueRead = false;

        public override void Serialize(byte b, List<byte> result) => result.Add(b);
        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            if (_isValueRead)
                return bufferIndex;
            Value = buffer[bufferIndex++];
            _isValueRead = true;
            return bufferIndex;
        }
        
        public override void Reset()
        {
            _isValueRead = false;
        }

        public override object GetValue()
        {
            return Value;
        }
    }
}