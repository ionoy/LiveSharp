using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace LiveSharp.Common.Events
{
    public static class EventExtensions
    {
        private static IReadOnlyList<Type> CommonEvents =
            new[] { typeof(WorkspaceEvent) };

        public static string Serialize(this Event evt)
        {
            using (var writer = new StringWriter())
            {
                new XmlSerializer(evt.GetType()).Serialize(writer, evt);
                return writer.ToString();
            }
        }

        public static Event DeserializeEvent(this string xmlString)
        {
            var doc = XDocument.Parse(xmlString);
            var eventTypeName = doc.Root.Name.LocalName;
            var eventType = CommonEvents.FirstOrDefault(e => e.Name == eventTypeName);

            if (eventType == null)
                throw new NotImplementedException($"Event type {eventTypeName} not supported for serialization/deserialization");

            using (var reader = new StringReader(xmlString))
            {
                var obj = new XmlSerializer(eventType).Deserialize(reader);
                return (Event)obj;
            }
        }
    }
}
