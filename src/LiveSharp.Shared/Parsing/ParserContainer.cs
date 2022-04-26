using System;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public abstract class ParserContainer
    {
        public class Instance : ParserContainer
        {
            public StreamingParser Parser { get; }

            public Instance(StreamingParser parser) => Parser = parser;
        }

        public class Factory : ParserContainer
        {
            public Func<StreamingParser, StreamingParser> ParserFactory { get; }

            public Factory(Func<StreamingParser, StreamingParser> parserFactory) => ParserFactory = parserFactory;
        }
    }
}