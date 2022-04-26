using System;
using System.Collections.Generic;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    class MultipleElementParser<TElement> : StreamingParser<IReadOnlyList<TElement>>
    {
        private readonly int _elementCount;
        private readonly StreamingParser<TElement> _elementParser;
        private readonly List<TElement> _values = new ();

        public MultipleElementParser(int elementCount, StreamingParser<TElement> elementParser)
        {
            _elementCount = elementCount;
            _elementParser = elementParser;
        }

        public override void Serialize(IReadOnlyList<TElement> elements, List<byte> result)
        {
            foreach (var element in elements) 
                _elementParser.Serialize(element, result);
        }

        public override bool IsParsingComplete => _values.Count == _elementCount;
        public override void Reset()
        {
            _values.Clear();
        }

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            while (true) {
                if (IsParsingComplete || bufferIndex >= buffer.Count)
                    break;
            
                bufferIndex = _elementParser.Feed(buffer, bufferIndex);
                
                if (_elementParser.IsParsingComplete) {
                    _values.Add((TElement)_elementParser.GetValue());
                    _elementParser.Reset();
                }
            }

            return bufferIndex;
        }

        public override object GetValue()
        {
            return _values.ToArray();
        }

        public TElement[] GetElementsValue() => (TElement[])GetValue();
    }
}