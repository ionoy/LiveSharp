using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Virtual;
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.ServerClient;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace LiveSharp.Runtime
{
    public class LiveSharpAssemblyContext
    {
        public LiveSharpAssemblyContextRegistry Registry { get; }
        public string FullName { get; }
        public Assembly Assembly => (Assembly)_assembly.Target;
        public bool IsAlive => _assembly.IsAlive;
        public ConcurrentDictionary<string, VirtualMethodInfo> AllMethods { get; } = new();
        public ConcurrentDictionary<string, VirtualFieldInfo> AllFields { get; } = new();
        public ConcurrentDictionary<string, VirtualPropertyInfo> AllProperties { get; } = new();

        private static int _metadataTokenCounter;
        private static ConcurrentDictionary<int, IVirtualMemberInfo> _virtualMembers = new();
        
        private readonly WeakReference _assembly;
        private readonly ConcurrentDictionary<string, DelegateSignature> _delegateSignatures = new();
        private readonly ConcurrentDictionary<string, DelegateFieldInfo> _precompiledDelegateFieldNameMapping = new();
        private readonly ConcurrentDictionary<string, (FieldInfo delegateField, FieldInfo hasUpdateField)> _delegateFieldCache = new();

        private readonly ConcurrentDictionary<string, ConditionalWeakTable<object, object>> _propertyStorage = new();
        private readonly ConcurrentDictionary<string, ConditionalWeakTable<object, object>> _fieldStorage = new();
        
        private readonly ConcurrentDictionary<string, VirtualTypeInfo> _types = new();
        private readonly DynamicTypeProxy _dynamicTypeProxy = new();

        public LiveSharpAssemblyContext(Assembly assembly, LiveSharpAssemblyContextRegistry registry)
        {
            Registry = registry;
            _assembly = new WeakReference(assembly);
            
            FullName = assembly.FullName;
        }
        
        public void ProcessUpdate(VirtualTypeInfo virtualTypeInfo)
        {
            foreach (var virtualMethodInfo in virtualTypeInfo.VirtualMethods.Reverse()) {
                var (delegateField, versionField) = FindDelegateFields(virtualMethodInfo.MethodIdentifier);
                
                // runtime generated methods don't have fields assigned to them
                if (delegateField == null)
                    continue;
                
                // for Generic methods, store virtualMethodInfo here
                if (delegateField.FieldType == typeof(object))
                    delegateField.SetValue(null, virtualMethodInfo);
                
                var currentDelegateVersion = (int)versionField.GetValue(null);
                versionField.SetValue(null, currentDelegateVersion + 1);
                
                //var (delegateField, _) = GetDelegateFields(virtualMethodInfo.MethodIdentifier);
                // if (delegateField != null && delegateField.FieldType.IsDelegate())
                //     virtualMethodInfo.BackingDelegateField = delegateField;
            }
        }
        
        public void AddDelegateFieldMapping(Type declaringType, string methodName, string methodIdentifier, Type fieldHost, string fieldName, Type returnType,
            Type[] parameterTypes)
        {
            var delegateFieldInfo = new DelegateFieldInfo(declaringType, methodName, returnType, methodIdentifier, fieldHost, fieldName);
            
            _precompiledDelegateFieldNameMapping[methodIdentifier] = delegateFieldInfo;
            _delegateSignatures[methodIdentifier] = new DelegateSignature {
                ReturnType = returnType,
                ParameterTypes = parameterTypes
            };

            _delegateFieldCache.TryRemove(methodIdentifier, out var _);
            
            if (AllMethods.Count > 0) {
                // it's possible for method update to arrive before mapping has been established
                // we need to make sure to attach these delegates to static fields anyway

                if (AllMethods.TryGetValue(methodIdentifier, out var virtualMethodInfo)) {
                    var delegateField = fieldHost.GetField(fieldName, BindingFlags.Static | BindingFlags.Public);
                    if (delegateField == null)
                        throw new InvalidOperationException($"Couldn't find field {fieldName} on {fieldHost}");

                    var existingRuntimeDelegate = virtualMethodInfo.DelegateBuilder.GetDelegate();
                    
                    if (existingRuntimeDelegate.GetType() == delegateField.FieldType)
                        delegateField.SetValue(null, existingRuntimeDelegate);
                }
            }

            var interceptorHandler = Registry.GetInterceptorHandler(declaringType, methodName);
            if (interceptorHandler != null) {
                CreateCallInterceptors(declaringType, methodName, interceptorHandler, null);
            }
        }

        public void CreateCallInterceptors(Type declaringType, string methodName, MethodCallHandler interceptor,
            Type excludeType)
        {
            var injectedInterceptors = new HashSet<string>();
            
            foreach (var virtualMethodInfo in AllMethods.Values) {
                var methodDeclaringType = CompilerHelpers.ResolveVirtualType(virtualMethodInfo.DeclaringType);
                var baseTypeFits = declaringType.IsAssignableFrom(methodDeclaringType);
                var notExcluded = excludeType == null || !excludeType.IsAssignableFrom(methodDeclaringType);
                var typeMatches = baseTypeFits && notExcluded;
                var nameMatches = methodName == null || methodName == virtualMethodInfo.Name;
                
                if (nameMatches && typeMatches) {
                    var methodMetadata = virtualMethodInfo.DelegateBuilder;
                    
                    methodMetadata.SetInterceptor(interceptor);
                    
                    TryUpdateDelegateField(methodMetadata, methodMetadata.GetDelegate());
                    
                    injectedInterceptors.Add(virtualMethodInfo.MethodIdentifier);
                }
            }

            foreach (var delegateFieldInfo in _precompiledDelegateFieldNameMapping.Values) {
                var delegateMethodIdentifier = delegateFieldInfo.MethodIdentifier;
                var baseTypeFits = declaringType.IsAssignableFrom(delegateFieldInfo.MethodDeclaringType);
                var notExcluded = excludeType == null || !excludeType.IsAssignableFrom(delegateFieldInfo.MethodDeclaringType);
                var typeMatches = baseTypeFits && notExcluded;
                var nameMatches = methodName == null || delegateFieldInfo.MethodName == methodName;

                if (nameMatches && typeMatches && !injectedInterceptors.Contains(delegateMethodIdentifier)) {
                    var delegateField = delegateFieldInfo.FieldDeclaringType.GetField(delegateFieldInfo.FieldName, BindingFlags.Static | BindingFlags.Public);
                    if (delegateField == null) {
                        Registry.Logger.LogError($"Field '{delegateFieldInfo.FieldName}' is missing for {delegateMethodIdentifier}.");
                    } else {
                        var delegateType = delegateField.FieldType;

                        if (!_delegateSignatures.TryGetValue(delegateMethodIdentifier, out var delegateSignature))
                            throw new InvalidOperationException($"Couldn't find delegate signature for {delegateMethodIdentifier}");

                        // Don't set interceptors for generic methods for now
                        // Need to figure out how to do it properly
                        if (delegateType == typeof(object))
                            continue;
                        
                        // We don't have DelegateSignature.ParameterTypes for generic methods 
                        // because signature is created from <module> initializer and generic type 
                        // parameters are unavailable outside of their methods/types
                        // Probably need to replace DelegateSignature with something else.
                        // Getting parameter types from Delegate.Invoke method crashed in some cases.
                        if (!delegateType.IsGenericType) {
                            var interceptorDelegate = DelegateBuilder.CreateInterceptorOnly(delegateType, delegateMethodIdentifier, interceptor, delegateSignature);

                            delegateField.SetValue(null, interceptorDelegate);
                        }
                    }
                }
            }
        }

        public void TryUpdateDelegateField(DelegateBuilder delegateBuilder, Delegate @delegate)
        {
            var (delegateField, versionField) = FindDelegateFields(delegateBuilder.MethodIdentifier);
            
            if (delegateField != null && delegateField.FieldType == @delegate.GetType())
                delegateField.SetValue(null, @delegate);
        }

        public Type GetDelegateType(VirtualMethodInfo methodInfo)
        {
            var returnType = methodInfo.ReturnType;
            
            if (!methodInfo.IsGeneric) {
                var (delegateField, _) = FindDelegateFields(methodInfo.MethodIdentifier);
                if (delegateField != null) {
                    // only return if the return type hasn't changed. otherwise it's a completely new delegate type incompatible with the original field 
                    if (_delegateSignatures.TryGetValue(methodInfo.MethodIdentifier, out var signature) && signature.ReturnType == returnType)
                        return delegateField.FieldType;
                }
            }
            
            var parameterTypes = methodInfo.Parameters.Select(m => m.ParameterType);

            return GetDelegateType(methodInfo.IsStatic, parameterTypes, returnType);
        }
        
        public static Type GetDelegateType(bool isStatic, IEnumerable<Type> parameterTypes, Type returnType)
        {
            parameterTypes = parameterTypes.Append(returnType).Select(t => t.ResolveVirtualType());

            if (!isStatic)
                parameterTypes = new[] {typeof(object)}.Concat(parameterTypes); // prepend 'this' parameter

            var result = parameterTypes.ToArray();
            if (result.OfType<GenericTypeParameter>().Any())
                throw new InvalidOperationException("Generic type parameters are not allowed in delegate type");
            
            return Expression.GetDelegateType(result.ToArray());
        }

        private (FieldInfo delegateField, FieldInfo hasUpdateField) FindDelegateFields(string methodIdentifier)
        {
            if (!_delegateFieldCache.TryGetValue(methodIdentifier, out var delegateFields)) {
                if (_precompiledDelegateFieldNameMapping.TryGetValue(methodIdentifier, out var fieldInfo)) {
                    var delegateFieldName = fieldInfo.FieldName;
                    var delimiter = delegateFieldName.LastIndexOf('_');
                    var versionFieldName = delegateFieldName.Substring(0, delimiter) + "_version";
                    
                    var delegateField = fieldInfo.FieldDeclaringType.GetField(delegateFieldName, BindingFlags.Static | BindingFlags.Public);
                    var versionField = fieldInfo.FieldDeclaringType.GetField(versionFieldName, BindingFlags.Static | BindingFlags.Public);

                    if (delegateField == null) {
                        Registry.Logger.LogError($"Found delegate field identifier for method '{methodIdentifier}', but field '{delegateFieldName}' is missing.");
                        return (null, null);
                    }

                    _delegateFieldCache[methodIdentifier] = (delegateField, versionField);

                    return (delegateField, versionField);
                }

                return (null, null);
                // Delegate types created in runtime should already be in `delegateFieldCache`, so we don't search for them
            }

            return (delegateFields.delegateField, delegateFields.hasUpdateField);
        }

        public ConditionalWeakTable<object, object> GetPropertyStorage(VirtualTypeInfo virtualTypeInfo,
            string propertyName)
        {
            var propertyIdentifier = virtualTypeInfo.FullName + " " + propertyName;
            return _propertyStorage.GetOrAdd(propertyIdentifier, _ => new ConditionalWeakTable<object, object>());
        }

        public ConditionalWeakTable<object, object> GetFieldStorage(VirtualTypeInfo virtualTypeInfo, string fieldName)
        {
            var propertyIdentifier = virtualTypeInfo.FullName + " " + fieldName;
            return _fieldStorage.GetOrAdd(propertyIdentifier, _ => new ConditionalWeakTable<object, object>());
        }

        public DocumentMetadata Update(XElement element, Func<string, bool> methodFilter = null, bool debuggingEnabled = false)
        {
            var documentMetadata = new DocumentMetadata(element, this, Registry.Logger, methodFilter, debuggingEnabled: debuggingEnabled);
            return documentMetadata;
        }
        
        public void AddOrUpdateType(VirtualTypeInfo typeInfo) => _types[typeInfo?.FullName ?? "<fullname-null>"] = typeInfo;

        public VirtualTypeInfo GetVirtualTypeInfo(string typeName)
        {
            if (_types.TryGetValue(typeName, out var virtualTypeInfo))
                return virtualTypeInfo;
            return null;
        }

        public VirtualTypeInfo GetOrCreateVirtualTypeInfo(string typeName, bool isAsyncStateMachine, Type underlyingType = null)
        {
            if (!_types.TryGetValue(typeName, out var virtualTypeInfo))
                virtualTypeInfo = CreateVirtualTypeInfo(typeName, isAsyncStateMachine, underlyingType);

            return virtualTypeInfo;
        }

        private VirtualTypeInfo CreateVirtualTypeInfo(string typeName, bool isAsyncStateMachine, Type underlyingType = null)
        {
            underlyingType ??= KnownTypes.FindType(typeName, showErrorIfNotFound: false);

            //No compiled type found, meaning it was added in runtime
            if (underlyingType == null && isAsyncStateMachine)
                underlyingType = typeof(VirtualAsyncStateMachine);

            if (underlyingType == null) {
                Registry.Logger.LogDebug($"Type {typeName} not found, using the dynamic one");
                underlyingType = _dynamicTypeProxy.GetDynamicType(typeName);
            }

            if (underlyingType == null)
                throw new Exception("Unknown type " + typeName);

            var virtualTypeInfo = new VirtualTypeInfo(typeName, underlyingType, Registry);

            AddOrUpdateType(virtualTypeInfo);
            
            return virtualTypeInfo;
        }

        public VirtualFieldInfo CreateVirtualFieldInfo(string fieldName, VirtualTypeInfo virtualDeclaringType, Type fieldType, FieldAttributes fieldAttributes, ConditionalWeakTable<object,object> storage, FieldInfo compiledField)
        {
            var virtualFieldInfo = new VirtualFieldInfo(_metadataTokenCounter++, fieldName, virtualDeclaringType, fieldType, fieldAttributes, storage, compiledField, this);
            _virtualMembers[virtualFieldInfo.Token] = virtualFieldInfo;
            AllFields[virtualFieldInfo.FullName] = virtualFieldInfo;
            return virtualFieldInfo;
        }

        public VirtualMethodInfo CreateVirtualMethodInfo(DocumentMetadata documentMetadata, LiveSharpAssemblyContext assemblyContext, XElement element,
            ILogger logger)
        {
            var deserializer = new VirtualMethodInfoDeserializer(element, documentMetadata, assemblyContext, logger);
            var virtualMethodInfo = new VirtualMethodInfo (
                documentMetadata, 
                assemblyContext, 
                element, 
                null, 
                deserializer.Name, 
                deserializer.MethodIdentifier, 
                deserializer.VirtualDeclaringType, 
                deserializer.ReturnType, 
                deserializer.IsStatic, 
                deserializer.MaxStackSize, 
                deserializer.IsGeneric, 
                deserializer.GenericParameters, 
                deserializer.Parameters,
                () => new VirtualMethodInfoBodyDeserializer(documentMetadata, assemblyContext).DeserializeMethodBody(element, logger));
            
            _virtualMembers[virtualMethodInfo.Token] = virtualMethodInfo;
            AllMethods[virtualMethodInfo.MethodIdentifier] = virtualMethodInfo;
            
            return virtualMethodInfo;
        }
        
        public int CreateMetadataToken(IVirtualMemberInfo memberInfo) {
            var token = _metadataTokenCounter++;
            
            _virtualMembers[token] = memberInfo;
            
            return token;
        }

        public VirtualPropertyInfo CreateVirtualPropertyInfo(string propertyName, VirtualTypeInfo virtualDeclaringType, Type propertyType, PropertyInfo compiledProperty)
        {
            var virtualPropertyInfo = new VirtualPropertyInfo(_metadataTokenCounter++, propertyName, virtualDeclaringType, propertyType, compiledProperty);
            _virtualMembers[virtualPropertyInfo.Token] = virtualPropertyInfo;
            return virtualPropertyInfo;
        }

        public static IVirtualMemberInfo ResolveVirtualMember(int token)
        {
            if (_virtualMembers.TryGetValue(token, out var vmi))
                return vmi;

            throw new Exception($"Virtual member with token {token} not found");
        }
    }
}