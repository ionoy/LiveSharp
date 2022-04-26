using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Virtual;
using LiveSharp.ServerClient;
using LiveSharp.Shared;

namespace LiveSharp.Runtime.IL
{
    public class DocumentMetadata
    {
        public bool DebuggingEnabled { get; }
        public Dictionary<int, Type> Types { get; } = new();
        public HashSet<int> GenericParameterTypes { get; } = new();
        public List<VirtualMethodInfo> UpdatedMethods { get; } = new();
        
        private readonly Dictionary<int, VirtualTypeInfo> _virtualTypes = new();
        private readonly LiveSharpAssemblyContext _assemblyContext;
        private readonly ILogger _logger;

        public DocumentMetadata(XElement documentElement, LiveSharpAssemblyContext assemblyContext, ILogger logger, Func<string, bool> methodFilter = null, bool debuggingEnabled = false)
        {
            _assemblyContext = assemblyContext;
            _logger = logger;

            DebuggingEnabled = debuggingEnabled;

            _logger.LogDebug("Document received: " + documentElement);            
            
            DeserializeTypes(documentElement);
            
            _logger.LogDebug("Deserializing fields");
            DeserializeFields(documentElement);
            
            _logger.LogDebug("Deserializing methods");
            DeserializeMethods(documentElement, methodFilter);
            
            _logger.LogDebug("Deserializing properties");
            DeserializeProperties(documentElement);

            _logger.LogDebug("Processing updates");
            foreach (var virtualTypeInfo in _virtualTypes.Values)
                _assemblyContext.ProcessUpdate(virtualTypeInfo);

            var staticConstructors = new List<VirtualMethodInfo>();
            
            _logger.LogDebug("Initializing delegates");
            foreach (var updatedMethod in UpdatedMethods) {
                // we will instantiate generic delegates later
                var declaringTypeIsGeneric = updatedMethod.DeclaringType?.ContainsGenericParameters == true;
                if (updatedMethod.IsGeneric || declaringTypeIsGeneric || updatedMethod.ContainsGenericParameters)
                    continue;
                
                updatedMethod.DelegateBuilder.InitializeDelegate();
                
                if (updatedMethod.Name == ".cctor")
                    staticConstructors.Add(updatedMethod);
            }

            _logger.LogDebug("Calling static constructors");
            foreach (var staticConstructor in staticConstructors) {
                staticConstructor.DelegateBuilder.Invoke(null);
            }
        }

        private void DeserializeMethods(XElement documentElement, Func<string, bool> methodFilter)
        {
            var methodElements = documentElement.Elements("Method").ToArray();
            
            foreach (var element in methodElements) {
                var methodIdentifier = element.AttributeValueOrThrow("MethodIdentifier");
                
                if (methodFilter != null && !methodFilter(methodIdentifier))
                    continue;

                var virtualMethodInfo = _assemblyContext.CreateVirtualMethodInfo(this, _assemblyContext, element, _logger);

                _logger.LogMessage($"Received update for method {virtualMethodInfo.DeclaringType?.FullName}.{virtualMethodInfo.Name}");
                
                UpdatedMethods.Add(virtualMethodInfo);
            }
        }

        private void DeserializeProperties(XElement documentElement)
        {
            var properties = documentElement.Descendants().Where(e => e.Name == "P");
            
            foreach (var property in properties) {
                var propertyName = property.Attribute("Name")?.Value ?? "<unknown-property>";
                var propertyType = Types[int.Parse(property.AttributeValueOrThrow("Type"))];
                var declaringTypeToken = int.Parse(property.AttributeValueOrThrow("DeclaringType"));
                var virtualDeclaringType = GetVirtualDeclaringType(declaringTypeToken);
                var storage = _assemblyContext.GetPropertyStorage(virtualDeclaringType, propertyName);
                var compiledProperty = virtualDeclaringType.UnderlyingType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                var virtualPropertyInfo = _assemblyContext.CreateVirtualPropertyInfo(propertyName, virtualDeclaringType, propertyType, compiledProperty);

                virtualDeclaringType.VirtualProperties[virtualPropertyInfo.Name] = virtualPropertyInfo;

                _assemblyContext.AllProperties[virtualPropertyInfo.FullName] = virtualPropertyInfo;

                _logger.LogMessage($"Received update for property {virtualDeclaringType.Name}.{propertyName}");
            }
        }
        
        private void DeserializeFields(XElement documentElement)
        {
            var fields = documentElement.Descendants().Where(e => e.Name == "F");

            foreach (var field in fields) {
                var fieldName = field.AttributeValueOrThrow("Name");
                var fieldType = Types[int.Parse(field.AttributeValueOrThrow("Type"))];
                var declaringTypeToken = int.Parse(field.AttributeValueOrThrow("DeclaringType"));
                var virtualDeclaringType = GetVirtualDeclaringType(declaringTypeToken);
                var storage = _assemblyContext.GetFieldStorage(virtualDeclaringType, fieldName);
                var compiledField = virtualDeclaringType.UnderlyingType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

                if (fieldType != compiledField?.FieldType)
                    compiledField = null;

                var virtualFieldInfo = _assemblyContext.CreateVirtualFieldInfo(fieldName, virtualDeclaringType, fieldType, FieldAttributes.Public, storage, compiledField);
                
                if (field.Attribute("InitialValue") is var initialValue&& initialValue != null) 
                    virtualFieldInfo.SetInitialValue(Convert.FromBase64String(initialValue.Value));

                virtualDeclaringType.VirtualFields[virtualFieldInfo.Name] = virtualFieldInfo;
                
                _logger.LogMessage($"Received update for field {virtualDeclaringType.Name}.{fieldName}");
            }
        }

        public VirtualTypeInfo GetVirtualDeclaringType(int declaringTypeToken)
        {
            if (!_virtualTypes.TryGetValue(declaringTypeToken, out var virtualDeclaringType)) {
                var underlyingType = Types[declaringTypeToken];
                var isAsyncStateMachine = typeof(IAsyncStateMachine).IsAssignableFrom(underlyingType);
                
                virtualDeclaringType = _assemblyContext.GetOrCreateVirtualTypeInfo(underlyingType.FullName, isAsyncStateMachine, underlyingType);

                _virtualTypes[declaringTypeToken] = virtualDeclaringType;
                
                return virtualDeclaringType;
            }

            return virtualDeclaringType;
        }

        private void DeserializeTypes(XElement documentElement)
        {
            var typeElements = documentElement.Elements("Type").ToArray();
            foreach (var typeElement in typeElements) {
                var token = int.Parse(typeElement.AttributeValueOrThrow("Token"));
                var typeIsByReference = bool.Parse(typeElement.AttributeValueOrThrow("TypeIsByReference"));
                var assemblyName = typeElement.AttributeValueOrThrow("AssemblyFullName");
                var typeName = typeElement.AttributeValueOrThrow("TypeFullName");
                var arrayRanks = typeElement.AttributeValueOrThrow("ArrayRanks");
                var isGenericParameter = bool.Parse(typeElement.AttributeValueOrThrow("IsGenericParameter"));
                var genericArgumentsAttribute = typeElement.AttributeValueOrThrow("GenericArguments");
                var isAsyncStateMachine = bool.Parse(typeElement.AttributeValueOrThrow("IsAsyncStateMachine"));

                if (isGenericParameter) {
                    var genericParameterPosition = int.Parse(typeElement.AttributeValueOrThrow("GenericParameterPosition"));
                    var isMethodOwnerValue = bool.Parse(typeElement.AttributeValueOrThrow("GenericParameterOwnerIsMethod"));

                    Types[token] = new GenericTypeParameter(token, typeof(object), typeName, genericParameterPosition, isMethodOwnerValue);
                    GenericParameterTypes.Add(token);
                    continue;
                }
                
                Type type = null;
                
                if (assemblyName.StartsWith(_assemblyContext.Assembly.GetName().Name + ","))
                    type = _assemblyContext.Assembly.GetType(typeName);

                type ??= KnownTypes.FindType(typeName, assemblyName, showErrorIfNotFound: false) ?? _assemblyContext.GetOrCreateVirtualTypeInfo(typeName, isAsyncStateMachine);
                
                if (type == null)
                    throw new InvalidOperationException($"{typeName}, {assemblyName} not found");

                if (!string.IsNullOrWhiteSpace(genericArgumentsAttribute)) {
                    var tokens = genericArgumentsAttribute
                        .Split(',')
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(int.Parse)
                        .ToArray();
                    var hasCustomGenericTypeParameters = tokens.Any(t => GenericParameterTypes.Contains(t));
                    var genericArguments = tokens
                        .Select(t => Types[t].ResolveVirtualType())
                        .ToArray();
                    
                    if (!hasCustomGenericTypeParameters) {
                        type = type.MakeGenericType(genericArguments);
                    } else {
                        type = new GenericTypeInstance(type, genericArguments);
                    }
                }
                
                if (typeIsByReference)
                    type = type.MakeByRefType();
                
                var arrayRankStrings = arrayRanks.Split(',');
                for (int i = 0; i < arrayRankStrings.Length; i++) {
                    if (string.IsNullOrWhiteSpace(arrayRankStrings[i]))
                        continue;
                    var rank = int.Parse(arrayRankStrings[i]);
                    if (rank == 1)
                        type = type.MakeArrayType();
                    else
                        type = type.MakeArrayType(rank);
                }
                
                Types[token] = type;
                
                if (type is VirtualTypeInfo vti) 
                    _virtualTypes[token] = vti;
            }
        }
    }
    
    
}