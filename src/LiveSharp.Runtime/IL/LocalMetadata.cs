using System;

namespace LiveSharp.Runtime.IL
{
    public class LocalMetadata
    {
        public string LocalName { get; }
        public Type LocalType {
            get;
            set;
        }

        public LocalMetadata(string localName, Type localType)
        {
            LocalName = localName;
            LocalType = localType;
        }

        public override string ToString()
        {
            return $"{LocalName} ({LocalType})";
        }
    }
}