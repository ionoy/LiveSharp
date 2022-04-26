using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Xml.Linq;
using LiveSharp.Rewriters;

namespace LiveSharp.Rewriters.Serialization
{
    public class TypeRegistry
    {
        private readonly RewriteLogger _logger;
        private readonly ConcurrentDictionary<string, TypeElement> _typeReferenceStringCache = new ConcurrentDictionary<string, TypeElement>();
        private readonly ConcurrentDictionary<TypeElement, int> _typeElements = new ConcurrentDictionary<TypeElement, int>();
        private readonly ConcurrentDictionary<int, TypeElement> _typeElementsByToken = new ConcurrentDictionary<int, TypeElement>();

        private int _currentLastToken = -1;
        
        public TypeRegistry(RewriteLogger logger)
        {
            _logger = logger;
        }

        public TypeElement GetTypeElement(int token)
        {
            if (_typeElementsByToken.TryGetValue(token, out var typeElement))
                return typeElement;
            
            throw new InvalidOperationException($"Type with token {token} not found");
        }
        
        // public void LoadTypesFromDiff(XElement diffDocument)
        // {
        //     try {
        //         if (diffDocument == null) 
        //             throw new ArgumentNullException(nameof(diffDocument));
        //
        //         var types = diffDocument.Element("Types")?.Elements("Type");
        //         if (types == null) {
        //             _logger.LogWarning("No types found in the diff");
        //             return;
        //         }
        //         
        //         foreach (var type in types) {
        //             var token = int.Parse(type.AttributeValueOrThrow(nameof(TypeElement.Token)));
        //             var arrayRanks = type.AttributeValueOrThrow(nameof(TypeElement.ArrayRanks)).Split(',').Select(int.Parse).ToArray();
        //             var assemblyFullName = type.AttributeValueOrThrow(nameof(TypeElement.AssemblyFullName));
        //             var genericParameterTypeString = type.AttributeValueOrThrow(nameof(TypeElement.GenericArguments));
        //             var isGenericParameter = bool.Parse(type.AttributeValueOrThrow(nameof(TypeElement.IsGenericParameter)));
        //             var typeFullName = type.AttributeValueOrThrow(nameof(TypeElement.TypeFullName));
        //             var typeIsByReference = bool.Parse(type.AttributeValueOrThrow(nameof(TypeElement.TypeIsByReference)));
        //             var genericArgumentTypes = genericParameterTypeString.Split(',').Select(int.Parse).ToArray();
        //             
        //             var typeElement = new TypeElement(arrayRanks, typeIsByReference, assemblyFullName, typeFullName, genericArgumentTypes, isGenericParameter);
        //             
        //             _typeElements[typeElement] = token;
        //             _typeElementsByToken[token] = typeElement;
        //             _currentLastToken = token > _currentLastToken ? token : _currentLastToken;
        //         }
        //     }
        //     catch (Exception e) {
        //         _logger.LogError("Unable to populate types from diff", e);
        //     }
        // }

        public bool TryGetTypeElementByTypeReferenceString(string typeReferenceString, out TypeElement typeElement)
        {
            return _typeReferenceStringCache.TryGetValue(typeReferenceString, out typeElement);
        }

        public int AddAndCreateToken(TypeElement typeElement, string typeReferenceString)
        {
            if (_typeElements.TryGetValue(typeElement, out var token)) {
                _typeReferenceStringCache[typeReferenceString] = typeElement;
                return token;
            }

            var newToken = ++_currentLastToken;
            
            typeElement.SetToken(newToken);
            
            _typeReferenceStringCache[typeReferenceString] = typeElement;
            _typeElements[typeElement] = newToken;
            _typeElementsByToken[newToken] = typeElement;

            return newToken;
        }
    }
}