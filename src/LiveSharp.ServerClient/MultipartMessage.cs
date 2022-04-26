using System;
using System.Linq;

namespace LiveSharp.ServerClient
{
    public class MultipartMessage : ServerMessage
    {
        public ServerMessage[] Messages { get; }

        public MultipartMessage(int parameter, params ServerMessage[] messages)
        {
            Messages = messages;
            MessageType = MessageType.Broadcast;
            ContentType = ContentTypes.Multipart;
            Parameter = parameter;
        }

        public override byte[] CreateBuffer()
        {
            return Messages.SelectMany(m => m.CreateBuffer()).ToArray();
        }

        public override string GetContentText()
        {
            throw new InvalidOperationException("Can't get content text from a multipart message");
        }

        public override string ToString()
        {
            return "Multipart message";
        }
    }
}