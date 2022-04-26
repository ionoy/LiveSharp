using System;
using System.Runtime.Serialization;

namespace LiveSharp.VisualStudio.LiveXAML
{
    [Serializable]
    internal class MalformedXamlException : Exception
    {
        public MalformedXamlException()
        {
        }

        public MalformedXamlException(string message) : base(message)
        {
        }

        public MalformedXamlException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected MalformedXamlException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
