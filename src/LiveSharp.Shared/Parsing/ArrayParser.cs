using System;
using System.Collections.Generic;
using System.Linq;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class ArrayParser<T> : StreamingParser<IReadOnlyList<T>>
    {
        private readonly StreamingParser<T> _elementParser;
        private readonly CompositeParser _compositeParser;

        public ArrayParser(StreamingParser<T> elementParser)
        {
            _elementParser = elementParser;
            _compositeParser = CompositeParser.StartWith(new IntParser())
                .Next(intParser => {
                    var elementCount = intParser.GetIntValue();
                    return new MultipleElementParser<T>(elementCount, elementParser);
                })
                .Build();
        }

        public override void Serialize(IReadOnlyList<T> value, List<byte> result)
        {
            if (value != null) {
                new IntParser().Serialize(value.Count, result);
                new MultipleElementParser<T>(value.Count, _elementParser).Serialize(value, result);
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

        public override object GetValue() => GetArrayValue();

        public T[] GetArrayValue() => (T[])_compositeParser.GetValues().Last();
    }
}