using System.Xml.Linq;

namespace LiveSharp.VisualStudio.Services
{
    public abstract class SerializationResult
    {
        public class EmptyOrIgnored : SerializationResult {}
        
        public class Property : SerializationResult
        {
            public SerializationResult Get { get; set; }
            public SerializationResult Set { get; set; }
            public string PropertyName { get; set; }
            public XElement Initializer { get; set; }
            public XElement PropertyType { get; set; }
        }
        
        public class Field : SerializationResult
        {
            public string FieldName { get; set; }
            public XElement Initializer { get; set; }
            public XElement FieldType { get; set; }
        }
        
        public class MethodOrConstructor : SerializationResult
        {
            public XElement Body { get; }
            public string Name { get; }
            public string IdentifierWithoutTypeName { get; }
            public string ContainingTypeName { get; }

            public MethodOrConstructor(XElement body, string name, string identifierWithoutTypeName, string containingTypeName)
            {
                Body = body;
                Name = name;
                IdentifierWithoutTypeName = identifierWithoutTypeName;
                ContainingTypeName = containingTypeName;
            }
        }
        
    }
}