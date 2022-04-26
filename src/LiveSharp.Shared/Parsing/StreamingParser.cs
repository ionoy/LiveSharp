using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public abstract class StreamingParser<T> : StreamingParser
    {

        public abstract void Serialize(T value, List<byte> result);
    }
    
    public abstract class StreamingParser
    {
        public abstract bool IsParsingComplete { get; }

        public abstract void Reset();
        
        public abstract int Feed(IReadOnlyList<byte> buffer, int bufferIndex);

        public abstract object GetValue();
        
    }
}