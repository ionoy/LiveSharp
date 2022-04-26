using LiveSharp.Rewriters.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;
using System;
using System.Collections.Concurrent;
using TypeDefinition = Mono.Cecil.TypeDefinition;

namespace LiveSharp.Rewriters.Serialization
{
    public class DocumentSerializer
    {
        private readonly AssemblyDiff _assemblyDiff;
        private readonly TypeRegistry _typeRegistry;
        private readonly string _assemblyName;
        private Dictionary<int, TypeElement> Types { get; } = new();

        public ConcurrentDictionary<string, int> GenericParameterCache { get; } = new();
        
        public DocumentSerializer(AssemblyDiff assemblyDiff, TypeRegistry typeRegistry, string assemblyName)
        {
            _assemblyDiff = assemblyDiff;
            _typeRegistry = typeRegistry;
            _assemblyName = assemblyName;
        }
        
        public XElement Serialize()
        {
            var serializedFields = _assemblyDiff.NewFields.Select(SerializeField).ToArray();
            var methodSerializers = _assemblyDiff.NewMethods.Select(m => new MethodDefinitionSerializer(m, this));
            var serializedMethods = methodSerializers.Select(m => m.Serialize()).ToList();
            var typeElements = Types
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value.ToElement());

            var memberElements = serializedFields.Concat(serializedMethods);
            var assemblyNameAttribute = new XAttribute("AssemblyName", _assemblyName);
            
            return new XElement("Document", assemblyNameAttribute, typeElements, memberElements);
        }
        
        public int GetTypeToken(TypeReference typeReference, MethodReference containingMethod, TypeReference containingType)
        {
            if (typeReference is GenericParameter gp)
                typeReference = FindGenericParameterUpstream(gp, containingMethod, containingType);

            var typeIsByReference = typeReference.IsByReference;
            
            if (typeReference is ByReferenceType brt)
                typeReference = brt.ElementType;
            
            var (arrayRanks, elementType) = GetTypeArrayDimension(typeReference);
            
            typeReference = elementType;
            typeReference = ResolveGenericArguments(typeReference, containingMethod, containingType);
            
            var typeDefinition = typeReference.Resolve();
            var typeDescription = GetTypeDescription(typeReference, arrayRanks, typeIsByReference);

            if (typeReference is GenericParameter gp2) {
                var cacheKey = GetGenericParameterCacheKey(gp2);
                
                if (GenericParameterCache.TryGetValue(cacheKey, out var token))
                    return token;
                
                token = AddNewTypeElement(typeReference, containingMethod, containingType, typeDefinition, arrayRanks, typeIsByReference, typeDescription);

                GenericParameterCache[cacheKey] = token;

                return token;
            }
            
            if (_typeRegistry.TryGetTypeElementByTypeReferenceString(typeDescription, out var existingType)) {
                Types[existingType.Token] = existingType;

                foreach (var genericArgumentToken in existingType.GenericArguments)
                    Types[genericArgumentToken] = _typeRegistry.GetTypeElement(genericArgumentToken);

                return existingType.Token;
            }

            return AddNewTypeElement(typeReference, containingMethod, containingType, typeDefinition, arrayRanks, typeIsByReference, typeDescription);
        }
        private string GetGenericParameterCacheKey(GenericParameter genericParameter)
        {
            if (genericParameter.Owner is TypeReference tr)
                return $"t:{tr.FullName} {genericParameter.Position}";

            if (genericParameter.Owner is MethodReference mr)
                return $"m:{mr.GetMethodIdentifier()} {genericParameter.Position}";

            throw new NotImplementedException("Cannot get generic parameter cache key for '" + genericParameter.Owner + "'");
        }
        private int AddNewTypeElement(TypeReference typeReference, MethodReference containingMethod, TypeReference containingType, TypeDefinition typeDefinition, IReadOnlyList<int> arrayRanks, bool typeIsByReference, string typeDescription)
        {
            var typeElement = CreateTypeElement(typeReference, typeDefinition, containingMethod, containingType, arrayRanks, typeIsByReference);
            var token = _typeRegistry.AddAndCreateToken(typeElement, typeDescription);

            Types[typeElement.Token] = typeElement;
            
            return token;
        }

        private static string GetTypeDescription(TypeReference typeReference, IReadOnlyList<int> arrayRanks,
            bool typeIsByReference)
        {
            var description = typeReference.ToString();
            if (arrayRanks.Count > 0)
                description += "[" + string.Join(",", arrayRanks) + "]";
            if (typeIsByReference)
                description += "&";
            return description;
        }

        private TypeElement CreateTypeElement(TypeReference typeReference, 
            TypeDefinition typeDefinition,
            MethodReference containingMethod, 
            TypeReference containingType, 
            IReadOnlyList<int> arrayRanks,
            bool typeIsByReference)
        {
            if (typeReference is GenericParameter gp && typeDefinition == null) {
                var isDefinedOnMethod = gp.Owner is MethodReference;
                return new TypeElement(arrayRanks, typeIsByReference, "", gp.Name, isGenericParameter: true, isAsyncStateMachine: false, genericParameterPosition: gp.Position, genericParameterOwnerIsMethod: isDefinedOnMethod);
            }
            
            var isAsyncStateMachine = typeDefinition.Interfaces.Any(i => i.InterfaceType.FullName == "System.Runtime.CompilerServices.IAsyncStateMachine");
            var assemblyFullName = typeDefinition.Module.Assembly.FullName;
            var typeFullName = typeDefinition.FullName.Replace("/", "+");
            
            if (typeReference.IsGenericInstance) {
                var genericInstanceType = (GenericInstanceType)typeReference;
                var genericArguments = genericInstanceType.GenericArguments;
                var serializedGenericArguments = genericArguments.Select(arg => GetTypeToken(arg, containingMethod, containingType)).ToArray();

                return new TypeElement(arrayRanks, typeIsByReference, assemblyFullName, typeFullName, serializedGenericArguments, isAsyncStateMachine: isAsyncStateMachine);
            }

            return new TypeElement(arrayRanks, typeIsByReference, assemblyFullName, typeFullName, isAsyncStateMachine: isAsyncStateMachine);
        }

        private static (IReadOnlyList<int>, TypeReference) GetTypeArrayDimension(TypeReference type)
        {
            var ranks = new List<int>();
            
            while (type.IsArray && type is ArrayType arrayType) {
                ranks.Add(arrayType.Dimensions.Count);
                type = arrayType.ElementType;
            }

            return (ranks, type);
        }
        
        private static TypeReference ResolveGenericArguments(TypeReference type, MethodReference containingMethod, TypeReference containingType)
        {
            if (type is GenericParameter genericParameter) {
                return FindGenericParameterUpstream(genericParameter, containingMethod, containingType);
            }

            if (type is GenericInstanceType genericInstanceType && genericInstanceType.ContainsGenericParameter) {
                // Create a new GIT, so we don't wreck this type for other resolutions
                var elementType = genericInstanceType.ElementType; 
                var newGit = new GenericInstanceType(elementType);
                var genericArguments = genericInstanceType.GenericArguments;
                
                for (int i = 0; i < genericArguments.Count; i++) {
                    var genericArgument = genericArguments[i];
                    newGit.GenericArguments.Add(ResolveGenericArguments(genericArgument, containingMethod, containingType));
                }

                return newGit;
            }

            return type;
        }

        public static TypeReference FindGenericParameterUpstream(GenericParameter gp, MethodReference containingMethod, TypeReference containingType)
        {
            if (containingMethod is GenericInstanceMethod gim) {
                if (gim.ElementMethod.GenericParameters.IndexOf(gp) is var index && index != -1)
                    return gim.GenericArguments[index];
            }

            if (containingType is GenericInstanceType git) {
                if (git.ElementType.GenericParameters.IndexOf(gp) is var index && index != -1)
                    return git.GenericArguments[index];
            }

            return gp;
        }


        private XElement SerializeProperty(PropertyDefinition property)
        {
            var declaringType = GetTypeToken(property.DeclaringType, null, null);
            var type = GetTypeToken(property.PropertyType, null, property.DeclaringType);
            
            return new XElement("P", new XAttribute("Name", property.Name), new XAttribute("Type", type), new XAttribute("DeclaringType", declaringType));
        }

        private XElement SerializeField(FieldDefinition field)
        {
            var declaringType = GetTypeToken(field.DeclaringType, null, null);
            var type = GetTypeToken(field.FieldType, null,field.DeclaringType);
            
            if (field.InitialValue != null && field.InitialValue.Length > 0) 
                return new XElement("F", new XAttribute("Name", field.Name), new XAttribute("Type", type), new XAttribute("DeclaringType", declaringType), new XAttribute("InitialValue", Convert.ToBase64String(field.InitialValue)));
            
            return new XElement("F", new XAttribute("Name", field.Name), new XAttribute("Type", type), new XAttribute("DeclaringType", declaringType));
        }
    }
}