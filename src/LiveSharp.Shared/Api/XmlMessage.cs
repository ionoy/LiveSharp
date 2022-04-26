using System.IO;
using System.Xml.Serialization;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Api
#else
namespace LiveSharp.Shared.Api
#endif
{
    public abstract class XmlMessage<T>
    {
        public string Serialize()
        {
            var serializer = new XmlSerializer(typeof(T), "livesharp");
            using var writer = new StringWriter();
            serializer.Serialize(writer, this);
            return writer.ToString();
        }

        public static T Deserialize(string xml)
        {
            var serializer = new XmlSerializer(typeof(T), "livesharp");
            using var reader = new StringReader(xml);
            
            return (T)serializer.Deserialize(reader);
        }
    }
}