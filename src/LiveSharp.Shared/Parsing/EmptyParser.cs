using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    class EmptyParser<TValue> : StreamingParser<object> where TValue : new()
    {
        public override bool IsParsingComplete => true;
        public override void Reset() { }

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex) => bufferIndex;
        public override void Serialize(object value, List<byte> result)
        {
        }

        public override object GetValue()
        {
            return new TValue();
        }
    }
}