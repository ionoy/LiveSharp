using System;
using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class Serialize
    {
        public static IReadOnlyList<byte> ObjectArray<T>(IReadOnlyList<T> objects, StreamingParser<T> elementParser)
        {
            var result = new List<byte>();
            new ArrayParser<T>(elementParser).Serialize(objects, result);
            return result;
        }
        
        public static IReadOnlyList<byte> Object<T>(T obj, StreamingParser<T> parser)
        {
            var result = new List<byte>();
            parser.Serialize(obj, result);
            return result;
        }
    }
}