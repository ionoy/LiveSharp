using System;
using System.Collections.Generic;
using System.Linq;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class CompositeParser : StreamingParser
    {
        private readonly List<ParserContainer> _containers = new();
        private StreamingParser[] _parsers;
        private int _currentParserIndex;

        private void AddContainer(ParserContainer parser) => _containers.Add(parser);

        private void Initialize()
        {
            _parsers = new StreamingParser[_containers.Count];
            Reset();
        }

        public override bool IsParsingComplete {
            get {
                return _currentParserIndex == _containers.Count - 1 &&
                       _parsers[_currentParserIndex].IsParsingComplete;
            }
        }

        public override void Reset()
        {
            _currentParserIndex = 0;
            FetchNextParser();
        } 

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            var currentParser = _parsers[_currentParserIndex];

            bufferIndex = currentParser.Feed(buffer, bufferIndex);

            if (currentParser.IsParsingComplete) {
                if (!IsParsingComplete) {
                    _currentParserIndex++;
                    FetchNextParser();
                    bufferIndex = Feed(buffer, bufferIndex);
                }
            }

            return bufferIndex;
        }

        private void FetchNextParser()
        {
            var container = _containers[_currentParserIndex];
            
            if (container is ParserContainer.Instance instance) {
                instance.Parser.Reset();
                _parsers[_currentParserIndex] = instance.Parser;
            } else if (container is ParserContainer.Factory factory) {
                if (_currentParserIndex > 0) {
                    var previousParser = _parsers[_currentParserIndex - 1];
                    _parsers[_currentParserIndex] = factory.ParserFactory(previousParser);
                } else {
                    _parsers[_currentParserIndex] = factory.ParserFactory(null);
                }
            }
        }

        public object[] GetValues()
        {
            return _parsers.Select(p => p.GetValue()).ToArray();
        }

        public override object GetValue()
        {
            return GetValues();
        }

        public static CompositeParserBuilder<T> StartWith<T>(T parser) where T : StreamingParser
        {
            var composite = new CompositeParser();
            composite.AddContainer(new ParserContainer.Instance(parser));
            return new CompositeParserBuilder<T>(composite);
        }
    
        public struct CompositeParserBuilder<TParser> where TParser : StreamingParser
        {
            private readonly CompositeParser _composite;

            public CompositeParserBuilder(CompositeParser composite)
            {
                _composite = composite;
            }

            public CompositeParserBuilder<TNextParser> Next<TNextParser>(Func<TParser, TNextParser> parserFactory) where TNextParser : StreamingParser
            {
                _composite.AddContainer(new ParserContainer.Factory(prev => parserFactory((TParser) prev)));
                return new CompositeParserBuilder<TNextParser>(_composite);
            }

            public CompositeParserBuilder<TNextParser> Next<TNextParser>(TNextParser parserInstance) where TNextParser : StreamingParser
            {
                _composite.AddContainer(new ParserContainer.Instance(parserInstance));
                return new CompositeParserBuilder<TNextParser>(_composite);
            }

            public CompositeParser Build()
            {
                _composite.Initialize();
                return _composite;
            }
        }
    }
}