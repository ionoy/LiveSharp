using System;
using System.Xml.Linq;

namespace LiveSharp.Runtime.Infrastructure
{
    public static class XElementExtensions
    {
        public static string AttributeValueOrThrow(this XElement instance, string attributeName)
        {
            return instance.Attribute(attributeName)?.Value ?? throw new Exception($"No attribute named {attributeName} on: {instance}");
        }
    }
}