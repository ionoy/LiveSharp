using System;
using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class LongParser : StreamingParser<long>
    {
        private readonly byte[] _internalBuffer = new byte[8];
        private int _internalBufferIndex;

        public override bool IsParsingComplete => _internalBufferIndex == _internalBuffer.Length;

        public override void Serialize(long e, List<byte> result)
        {
            result.AddRange(BitConverter.GetBytes(e));
        }
        
        public override void Reset()
        {
            _internalBufferIndex = 0;
        }

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            for (; bufferIndex < buffer.Count;) {
                _internalBuffer[_internalBufferIndex++] = buffer[bufferIndex++];

                if (IsParsingComplete)
                    break;
            }

            return bufferIndex;
        }

        public override object GetValue()
        {
            return GetLongValue();
        }

        public long GetLongValue()
        {
            return BitConverter.ToInt64(_internalBuffer, 0);
        }
    }
}