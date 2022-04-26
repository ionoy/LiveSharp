using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Virtual;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace LiveSharp.Runtime.IL
{
    public class VirtualMethodInfoBodyDeserializer
    {
        public List<LocalMetadata> Locals { get; }
        public Dictionary<string, MemberInfo> Members { get; }
        public IDictionary<string, string> Strings { get; }
        public string IL { get; }
        public VirtualMethodBody MethodBody { get; }
        
        private readonly DocumentMetadata _documentMetadata;
        private readonly LiveSharpAssemblyContext _assemblyContext;
        
        public VirtualMethodInfoBodyDeserializer(DocumentMetadata documentMetadata, LiveSharpAssemblyContext assemblyContext)
        {
            _documentMetadata = documentMetadata;
            _assemblyContext = assemblyContext;
        }

        public VirtualMethodBody DeserializeMethodBody(XElement methodElement, ILogger logger)
        {
            var members = DeserializeMembers(methodElement.Descendants("Member"));
            var locals = DeserializeLocals(methodElement.Descendants("Local"));
            var strings = DeserializeStrings(methodElement.Element("Strings")?.Elements("S") ?? new XElement[0]);
            var il = methodElement.Descendants("IL").FirstOrDefault()?.Value ?? "";
            
            var tryBlocks = DeserializeExceptionHandlers(methodElement.Descendants("Try"));
            var instructions = IlParser.Parse(members, locals, strings, il, _documentMetadata.Types);

            instructions.AddTryBlocks(tryBlocks);

            return new VirtualMethodBody(_documentMetadata, instructions, logger);
        }
        
        private List<LocalMetadata> DeserializeLocals(IEnumerable<XElement> variableElements)
        {
            var result = new List<LocalMetadata>();

            foreach (var variableElement in variableElements) {
                var name = variableElement.AttributeValueOrThrow("Name");
                var typeToken = int.Parse(variableElement.AttributeValueOrThrow("Type"));
                var type = _documentMetadata.Types[typeToken];

                result.Add(new LocalMetadata(name, type));
            }

            return result;
        }

        private Dictionary<string, object> DeserializeMembers(IEnumerable<XElement> memberElements)
        {
            var result = new Dictionary<string, object>();

            foreach (var memberElement in memberElements) {
                var memberToken = memberElement.AttributeValueOrThrow("Token");
                var containingTypeToken = int.Parse(memberElement.AttributeValueOrThrow("ContainingType"));
                var memberName = memberElement.AttributeValueOrThrow("Name");
                var declaringType = _documentMetadata.Types[containingTypeToken];
                var memberType = memberElement.AttributeValueOrThrow("MemberType");

                if (memberType == "Method") {
                    var parameterTypesValue = memberElement.AttributeValueOrThrow("ParameterTypes");
                    var genericArgumentsAttribute = memberElement.Attribute("GenericArguments");

                    var returnType = _documentMetadata.Types[int.Parse(memberElement.AttributeValueOrThrow("ReturnType"))];
                    var (_, parameterTypes) = ResolveParameterTypes(parameterTypesValue);
                    var genericArguments = genericArgumentsAttribute?.Value != null
                        ? genericArgumentsAttribute?.Value.Split(',').Select(tToken => _documentMetadata.Types[int.Parse(tToken)]).ToArray()
                        : new Type[0];
                    
                    if (declaringType is GenericTypeInstance) {
                        result[memberToken] = new GenericTypeResolverEval(resolver => {
                            var resolvedGenericType = resolver.ResolveGenericType(declaringType);
                            var resolvedParameterTypes = parameterTypes.Select(resolver.ResolveGenericType).ToArray();
                            var resolvedGenericArguments = genericArguments.Select(resolver.ResolveGenericType).ToArray();
                            var resolvedReturnType = resolver.ResolveGenericType(returnType);
                            
                            return ResolveMethodMember(resolvedGenericType, memberName, resolvedReturnType, resolvedParameterTypes, resolvedGenericArguments);
                        }, declaringType);
                    } else {
                        result[memberToken] = ResolveMethodMember(declaringType, memberName, returnType, parameterTypes, genericArguments);
                    }
                } else if (memberType == "Property") {
                    var type = int.Parse(memberElement.AttributeValueOrThrow("Type"));
                    result[memberToken] = ResolvePropertyMember(declaringType, memberName, _documentMetadata.Types[type]);
                } else if (memberType == "Field") {
                    var fieldTypeToken = int.Parse(memberElement.AttributeValueOrThrow("Type"));
                    var fieldType = _documentMetadata.Types[fieldTypeToken];
                    
                    if (declaringType is GenericTypeInstance) {
                        result[memberToken] = new GenericTypeResolverEval(resolver => {
                            var resolvedGenericType = resolver.ResolveGenericType(declaringType);
                            var resolvedFieldType = resolver.ResolveGenericType(fieldType);
                            
                            return ResolveFieldMember(resolvedGenericType, memberName, resolvedFieldType);
                        });
                    } else {
                        result[memberToken] = ResolveFieldMember(declaringType, memberName, fieldType);
                    }
                } else if (memberType == "Event") {
                    result[memberToken] = ResolveEventMember(declaringType, memberName);
                } else {
                    throw new InvalidOperationException("Unknown member type: '" + memberType + "'");
                }
            }

            return result;
        }

        private MemberInfo ResolveEventMember(Type type, string memberName)
        {
            var ev = type
                .GetEvents(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(p => p.Name == memberName);

            if (ev != null)
                return ev;

            var eventIdentifier = type.FullName + "." + memberName;

            throw new InvalidOperationException("Couldn't find event: '" + eventIdentifier + "'");
        }

        private PropertyInfo ResolvePropertyMember(Type type, string memberName, Type propertyType)
        {
            var property = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(p => p.Name == memberName && (p.PropertyType == propertyType || p.PropertyType.IsGenericParameter));

            if (property != null)
                return property;

            var propertyIdentifier = type.FullName + "." + memberName;

            if (_assemblyContext.AllProperties.TryGetValue(propertyIdentifier, out var virtualProperty))
                return virtualProperty;

            throw new InvalidOperationException("Couldn't find property: '" + propertyIdentifier + "'");
        }

        private FieldInfo ResolveFieldMember(Type type, string memberName, Type fieldType)
        {
            var field = type
                .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(p => p.Name == memberName && IsSameType(p.FieldType, fieldType, new Type[0]));

            if (field != null)
                return field;

            if (type is VirtualTypeInfo vti) {
                var virtualField = vti
                    .VirtualFields
                    .Select(kvp => kvp.Value)
                    .FirstOrDefault(fi => fi.Name == memberName);
                
                if (virtualField != null)
                    return virtualField;
            }

            var fieldIdentifier = type.FullName + "." + memberName;

            if (_assemblyContext.AllFields.TryGetValue(fieldIdentifier, out var virtualProperty))
                return virtualProperty;
            
            throw new InvalidOperationException("Couldn't find field: '" + fieldIdentifier + "'");

        }

        private object ResolveMethodMember(Type declaringType, string memberName, Type returnType,
            Type[] parameterTypes, Type[] genericArguments)
        {
            if (declaringType is VirtualTypeInfo vti && typeof(VirtualTypeBase).IsAssignableFrom(vti.UnderlyingType)) {
                var resolvedVirtualMethod = ResolveVirtualMethodMember(vti, memberName, returnType, parameterTypes, genericArguments);
                if (resolvedVirtualMethod != null)
                    return resolvedVirtualMethod;
                // otherwise we might just need a default constructor, so let's search for that
            }
            
            var methodIdentifierWithoutParameters = declaringType.FullName + " " + memberName + " ";
            // This doesn't work if t.FullName returns something like System.Collections.Generic.IEnumerable`1[[A.B.C, A.B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]'
            var methodIdentifier = methodIdentifierWithoutParameters + string.Join(" ", parameterTypes.Select(t => t.FullName));

            var methods = declaringType.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .OfType<MethodBase>()
                .Where(m => m.Name == memberName)
                .ToArray();

            var method = GetCompatibleMethod(methods, memberName, parameterTypes, returnType, genericArguments, false);

            if (method != null)
                return ResolveGenericMethod(method, genericArguments);
            var candidates = _assemblyContext
                .AllMethods
                .Where(kvp => kvp.Key.StartsWith(methodIdentifierWithoutParameters))
                .Select(kvp => kvp.Value)
                .ToArray();

            // No need to filter by return type because source application won't allow return type overloading
            // this means that virtual method should also be updated to the new signature automatically
            // or? return type might have changed for the same method
            if (_assemblyContext.AllMethods.TryGetValue(methodIdentifier, out var virtualMethodInfo))
                return virtualMethodInfo;

            foreach (var candidate in candidates) {
                var candidateParameterTypes = candidate.GetParameterTypes();
                if (candidateParameterTypes.Length == parameterTypes.Length) {
                    var parameterTypesMatch = true;
                
                    for (int i = 0; i < candidateParameterTypes.Length; i++) {
                        var candidateParameterType = candidateParameterTypes[i].ResolveVirtualType();
                        var requiredParameterType = parameterTypes[i].ResolveVirtualType();
                    
                        if (IsSameType(candidateParameterType, requiredParameterType, genericArguments))
                            continue;

                        parameterTypesMatch = false;
                    
                        break;
                    }

                    if (parameterTypesMatch) {
                        if (candidate.IsGeneric) {
                            candidate.GenericArguments.AddRange(genericArguments);
                        }
                        
                        return candidate;
                    }
                }
            }

            // TODO proper virtual generic method resolution
            if (genericArguments.Length > 0 && candidates.Length > 0)
                return candidates.First();

            if (memberName.StartsWith("set_"))
                return ResolveMethodMember(declaringType, "put_" + memberName.Substring(4), returnType, parameterTypes, genericArguments);

            throw new InvalidOperationException("Couldn't find method: '" + methodIdentifier + "'");
        }
        
        private object ResolveGenericMethod(object method, Type[] genericArguments)
        {
            if (method is MethodInfo mi && mi.IsGenericMethodDefinition) {
                genericArguments = genericArguments.Select(a => a.ResolveVirtualType()).ToArray();

                return mi.MakeGenericMethod(genericArguments);
            }

            return method;
        }

        private object GetCompatibleMethod(IEnumerable<MethodBase> methodList, string methodName, Type[] requiredParameterTypes, Type requiredReturnType, Type[] genericTypeArguments = null, bool throwIfNotFound = true)
        {
            genericTypeArguments ??= new Type[0];

            genericTypeArguments = genericTypeArguments.Select(a => a.ResolveVirtualType()).ToArray();
            
            foreach (var method in methodList) {
                if (method.Name != methodName)
                    continue;

                var candidateParameters = method.GetParameters();
                
                if (candidateParameters.Length != requiredParameterTypes.Length)
                    continue;
                
                if (method is MethodInfo mi2 && !IsSameType(mi2.ReturnType.ResolveVirtualType(), requiredReturnType.ResolveVirtualType(), genericTypeArguments))
                    continue;
                
                if (method.IsGenericMethod && method.GetGenericArguments().Length != genericTypeArguments.Length)
                    continue;
                
                var parameterTypesMatch = true;
                
                for (int i = 0; i < candidateParameters.Length; i++) {
                    var candidateParameterType = candidateParameters[i].ParameterType.ResolveVirtualType();
                    var requiredParameterType = requiredParameterTypes[i].ResolveVirtualType();
                    
                    if (IsSameType(candidateParameterType, requiredParameterType, genericTypeArguments))
                        continue;

                    parameterTypesMatch = false;
                    
                    break;
                }

                if (parameterTypesMatch)
                    return method;
            }

            if (throwIfNotFound)
                throw new Exception("MethodInfo not found " + methodName + " (" + string.Join(", ", requiredParameterTypes.Select(t => t.Name)) + ")");

            return null;
        }

        private bool IsSameType(Type l, Type r, Type[] genericTypeArguments)
        {
            if (l == r)
                return true;

            if (l.IsByRef && r.IsByRef)
                return IsSameType(l.GetElementType(), r.GetElementType(), genericTypeArguments);

            if (l.IsArray && r.IsArray)
                return IsSameType(l.GetElementType(), r.GetElementType(), genericTypeArguments);

            if (l.IsPointer && r.IsPointer)
                return IsSameType(l.GetElementType(), r.GetElementType(), genericTypeArguments);

            if (l.IsGenericParameter) {
                if (r.IsGenericParameter) {
                    // we don't need to compare generic parameters
                    // because one might be a parameter and another an argument
                    return true;
                }

                return IsSameType(genericTypeArguments[l.GenericParameterPosition], r, genericTypeArguments);
            }

            if (l.IsGenericType && r.IsGenericType) {
                var lTypeDefinition = l.IsGenericTypeDefinition ? l : l.GetGenericTypeDefinition();
                var rTypeDefinition = r.IsGenericTypeDefinition ? r : r.GetGenericTypeDefinition();
                
                if (lTypeDefinition != rTypeDefinition)
                    return false;
                
                var lGenericArgs = l.GetGenericArguments();
                var rGenericArgs = r.GetGenericArguments();

                if (lGenericArgs.Length != rGenericArgs.Length)
                    return false;
                
                for (var index = 0; index < lGenericArgs.Length; index++)
                    if (!IsSameType(lGenericArgs[index], rGenericArgs[index], genericTypeArguments))
                        return false;

                return true;
            }

            return false;
        }
        
        private IDictionary<string, string> DeserializeStrings(IEnumerable<XElement> strings)
        {
            var result = new Dictionary<string, string>();

            foreach (var str in strings) {
                result[str.AttributeValueOrThrow("Id")] = str.Value;
            }

            return result;
        }
        
        private (int[], Type[]) ResolveParameterTypes(string parameterTypesValue)
        {
            if (parameterTypesValue == null)
                throw new InvalidOperationException("Missing ParameterTypes attribute");

            var parameterTypeTokens = parameterTypesValue
                .Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();

            var parameterTypes = parameterTypeTokens
                .Select(tToken => _documentMetadata.Types[tToken])
                .ToArray();

            return (parameterTypeTokens, parameterTypes);
        }

        private MethodBase ResolveVirtualMethodMember(VirtualTypeInfo vti, string memberName, Type returnType, Type[] parameterTypes, Type[] genericArguments)
        {
            var candidates = vti.VirtualMethods.Where(m => m.Name == memberName && m.ReturnType == returnType);

            foreach (var candidate in candidates) {
                var candidateParameterTypes = candidate.GetParameterTypes();

                if (candidateParameterTypes.Length != parameterTypes.Length)
                    continue;

                var typesDiffer = false;

                for (int i = 0; i < candidateParameterTypes.Length; i++) {
                    if (candidateParameterTypes[i] != parameterTypes[i]) {
                        typesDiffer = true;
                        break;
                    }
                }

                if (!typesDiffer)
                    return candidate;
            }

            return null;
        }
        
        private TryBlock[] DeserializeExceptionHandlers(IEnumerable<XElement> tryBlockElements)
        {
            return tryBlockElements.Select(deserializeTryBlock).ToArray();

            TryBlock deserializeTryBlock(XElement tryBlock)
            {
                var tryStart = int.Parse(tryBlock.AttributeValueOrThrow("Start"), CultureInfo.InvariantCulture);
                var tryEnd = int.Parse(tryBlock.AttributeValueOrThrow("End"), CultureInfo.InvariantCulture);
                var handler = tryBlock.Descendants("Handler").First();
                var handlerType = handler.AttributeValueOrThrow("Type");
                var catchType = int.Parse(handler.AttributeValueOrThrow("CatchType"), CultureInfo.InvariantCulture);
                var handlerStart = int.Parse(handler.AttributeValueOrThrow("Start"), CultureInfo.InvariantCulture);
                var handlerEnd = int.Parse(handler.AttributeValueOrThrow("End"), CultureInfo.InvariantCulture);
                var filterStart = int.Parse(handler.AttributeValueOrThrow("FilterStart"), CultureInfo.InvariantCulture);
                var resolvedCatchType = catchType != -1 ? _documentMetadata.Types[catchType] : null;

                return new TryBlock(
                    tryStart,
                    tryEnd,
                    handlerType,
                    handlerStart,
                    handlerEnd,
                    filterStart,
                    resolvedCatchType);
            }
        }

        public bool TryGetTypeByToken(int typeToken, out Type type) => _documentMetadata.Types.TryGetValue(typeToken, out type);
    }
}