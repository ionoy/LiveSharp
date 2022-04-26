using System;
using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class Deserialize
    {
        public static T Object<T>(IReadOnlyList<byte> buffer, StreamingParser<T> parser)
        {
            parser.Feed(buffer, 0);

            if (parser.IsParsingComplete)
                return (T)parser.GetValue();

            throw new InvalidOperationException($"Invalid buffer for parser of type {typeof(T)}");
        }
        
        public static T Object<T>(IReadOnlyList<byte> buffer) where T : new()
        {
            var objectParser = new ObjectParser<T>();
            objectParser.Feed(buffer, 0);

            if (objectParser.IsParsingComplete)
                return objectParser.GetObjectValue();

            throw new InvalidOperationException($"Invalid buffer for parser of type {typeof(T)}");
        }
        
        public static IReadOnlyList<T> ObjectArray<T>(IReadOnlyList<byte> buffer, StreamingParser<T> elementParser)
        {
            var objectParser = new ArrayParser<T>(elementParser);
            objectParser.Feed(buffer, 0);

            if (objectParser.IsParsingComplete)
                return objectParser.GetArrayValue();

            throw new InvalidOperationException($"Invalid buffer for parser of type {typeof(T)}");
        }
    }
}