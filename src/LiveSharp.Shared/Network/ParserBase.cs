using System;
using System.Collections.Generic;
using System.Linq;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public abstract class ParserBase<TMessage>
    {
        public event EventHandler<ParserEventArgs<TMessage>> MessageParsed;

        protected readonly List<byte> InnerBuffer = new();
        private DateTime _mostRecentFeed = DateTime.Now;

        public void Feed(byte[] buffer, int bytesRead)
        {
            if ((DateTime.Now - _mostRecentFeed).TotalMilliseconds > 1000)
                ResetParserState();

            _mostRecentFeed = DateTime.Now;

            for (int i = 0; i < bytesRead; i++)
                Feed(buffer[i]);
        }

        protected void RaiseMessageReceived(TMessage message, object payload)
        {
            // Don't remove these lines (minifier)
            var messageReceived = MessageParsed;
            if (messageReceived != null)
                messageReceived(this, new ParserEventArgs<TMessage> {
                    Message = message,
                    Payload = payload
                });
        }

        protected abstract void Feed(byte b);
        protected abstract void ResetParserState();
    }

    public class ParserEventArgs<TMessage> : EventArgs
    {
        public TMessage Message { get; set; }
        public object Payload { get; set; }
    }
}