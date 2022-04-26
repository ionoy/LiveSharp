using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Virtual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace LiveSharp.Runtime.IL
{

    public class VirtualMethodInfoDeserializer
    {
        private readonly DocumentMetadata _documentMetadata;
        private readonly LiveSharpAssemblyContext _assemblyContext;
        public ParameterMetadata[] Parameters { get; }

        public bool IsGeneric { get; }

        public int MaxStackSize { get; }

        public bool IsStatic { get; }

        public Type ReturnType { get; }

        public VirtualTypeInfo VirtualDeclaringType { get; }

        public string MethodIdentifier { get; }
        public string Name { get; }

        public VirtualMethodInfoDeserializer(XElement methodElement, DocumentMetadata documentMetadata, LiveSharpAssemblyContext assemblyContext, ILogger logger)
        {
            _documentMetadata = documentMetadata;
            _assemblyContext = assemblyContext;

            Parameters = DeserializeParameters(methodElement.Descendants("Parameter"));
            GenericParameters = (methodElement.Element("GenericParameters")?.Value ?? "")
                .Split(',')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select<string, Type>(s => resolveParameterType(int.Parse(s)))
                .ToArray();

            Name = methodElement.AttributeValueOrThrow("Name");
            MethodIdentifier = methodElement.AttributeValueOrThrow("MethodIdentifier");
            VirtualDeclaringType = documentMetadata.GetVirtualDeclaringType(int.Parse(methodElement.AttributeValueOrThrow("DeclaringType")));
            ReturnType = documentMetadata.Types[int.Parse(methodElement.AttributeValueOrThrow("ReturnType"))];
            IsStatic = bool.Parse(methodElement.AttributeValueOrThrow("IsStatic"));
            MaxStackSize = int.Parse(methodElement.AttributeValueOrThrow("MaxStackSize"));
            IsGeneric = bool.Parse(methodElement.AttributeValueOrThrow("IsGeneric"));
            
            Type resolveParameterType(int token) => documentMetadata.Types[token];
        }

        public Type[] GenericParameters { get; set; }

        private ParameterMetadata[] DeserializeParameters(IEnumerable<XElement> variableElements)
        {
            var result = new List<ParameterMetadata>();

            foreach (var variableElement in variableElements) {
                var name = variableElement.AttributeValueOrThrow("Name");
                var typeToken = int.Parse(variableElement.AttributeValueOrThrow("Type"));
                var type = _documentMetadata.Types[typeToken];

                result.Add(new ParameterMetadata(name, type));
            }

            return result.ToArray();
        }
    }
}