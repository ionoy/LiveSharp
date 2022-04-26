using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class BoolParser : StreamingParser<bool>
    {
        public bool Value { get; private set; }
        public override bool IsParsingComplete => _isValueRead;
        
        private bool _isValueRead = false;

        public override void Serialize(bool b, List<byte> result) => result.Add((byte)(b ? 1 : 0));
        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            if (_isValueRead)
                return bufferIndex;
            Value = buffer[bufferIndex++] == 1;
            _isValueRead = true;
            return bufferIndex;
        }
        
        public override void Reset()
        {
            _isValueRead = false;
        }

        public override object GetValue()
        {
            return GetBoolValue();
        }

        public bool GetBoolValue()
        {
            return Value;
        }
    }
}