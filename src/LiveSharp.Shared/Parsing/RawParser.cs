using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class RawParser : StreamingParser<byte[]>
    {
        private readonly byte[] _internalBuffer;
        private int _internalBufferIndex;

        public RawParser(int bufferLength)
        {
            _internalBuffer = new byte[bufferLength];
        }

        public override bool IsParsingComplete => _internalBufferIndex == _internalBuffer.Length;

        public override void Serialize(byte[] bytes, List<byte> result)
        {
            result.AddRange(bytes);
        }

        public override void Reset()
        {
            _internalBufferIndex = 0;
        }

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            for (; !IsParsingComplete && bufferIndex < buffer.Count;) {
                _internalBuffer[_internalBufferIndex++] = buffer[bufferIndex++];
            }

            return bufferIndex;
        }

        public IReadOnlyList<byte> GetBufferValue() => _internalBuffer;
        public override object GetValue() => GetBufferValue();
    }
}