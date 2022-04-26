using System;
using System.Collections.Generic;
using System.Linq;
#if LIVESHARP_RUNTIME
using LiveSharp.Runtime.Debugging;
using LiveSharp.Runtime.Parsing;

namespace LiveSharp.Runtime.Parsing
#else
using LiveSharp.Shared.Debugging;
using LiveSharp.Shared.Parsing;

namespace LiveSharp.Shared.Parsing
#endif
{
    public class DebugEventParser : StreamingParser<DebugEvent>
    {
        private readonly CompositeParser _compositeParser;

        public DebugEventParser()
        {
            _compositeParser = CompositeParser.StartWith(new ByteParser())
                .Next<StreamingParser>(byteParser => {
                    var debugEventType = byteParser.Value;
                    if (debugEventType == 0)
                        return new ObjectParser<StartDebugEvent>();
                    if (debugEventType == 1)
                        return new ObjectParser<AssignDebugEvent>();
                    if (debugEventType == 2)
                        return new ObjectParser<ReturnDebugEvent>();

                    throw new InvalidOperationException("Unknown debug event type: " + debugEventType);
                })
                .Build();
        }

        public override void Serialize(DebugEvent debugEvent, List<byte> result)
        {
            if (debugEvent is StartDebugEvent sde) {
                new ByteParser().Serialize(0, result);
                new ObjectParser<StartDebugEvent>().Serialize(sde, result);
            } else if (debugEvent is AssignDebugEvent ade) {
                new ByteParser().Serialize(1, result);
                new ObjectParser<AssignDebugEvent>().Serialize(ade, result);
            } else if (debugEvent is ReturnDebugEvent rde) {
                new ByteParser().Serialize(2, result);
                new ObjectParser<ReturnDebugEvent>().Serialize(rde, result);
            }
        }

        public override bool IsParsingComplete => _compositeParser.IsParsingComplete;
        public override void Reset()
        {
            _compositeParser.Reset();
        }

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            return _compositeParser.Feed(buffer, bufferIndex);
        }

        public DebugEvent GetDebugEventValue()
        {
            return (DebugEvent)_compositeParser.GetValue();
        }

        public override object GetValue()
        {
            return _compositeParser.GetValues().LastOrDefault();
        }
    }
}