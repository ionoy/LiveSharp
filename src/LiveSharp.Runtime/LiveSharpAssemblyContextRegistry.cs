using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Virtual;
using LiveSharp.ServerClient;
using LiveSharp.Shared;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LiveSharp.Runtime
{
    public class LiveSharpAssemblyContextRegistry
    {
        private readonly List<LiveSharpAssemblyContext> _assemblyContexts = new();
        private readonly ConcurrentDictionary<(Type declaringType, string methodName), MethodCallHandler> _interceptorHandlers = new();

        public ILogger Logger => LiveSharpRuntime.Logger;

        public LiveSharpAssemblyContext AddAssembly(Assembly assembly)
        {
            lock (_assemblyContexts) {
                var liveSharpAssemblyContext = new LiveSharpAssemblyContext(assembly, this);
                _assemblyContexts.Add(liveSharpAssemblyContext);
                return liveSharpAssemblyContext;
            }
        }
        
        public void AddAssemblyLoadContext(ILiveSharpLoadContext assemblyLoadContext)
        {
            lock (_assemblyContexts) {
                _assemblyContexts.Add(new LiveSharpAssemblyContext(assemblyLoadContext.MainAssembly, this));
                foreach (var referenceAssembly in assemblyLoadContext.ReferenceAssemblies) {
                    _assemblyContexts.Add(new LiveSharpAssemblyContext(referenceAssembly, this));
                }
            }
        }

        public LiveSharpAssemblyContext GetLatestAssembly(string assemblyFullName)
        {
            lock (_assemblyContexts) {
                Cleanup();

                for (int i = _assemblyContexts.Count - 1; i >= 0; i--) {
                    var assemblyContext = _assemblyContexts[i];
                    var assembly = assemblyContext.Assembly;

                    if (assembly?.FullName == assemblyFullName)
                        return assemblyContext;
                }
            }

            return null;
        }

        public IReadOnlyList<LiveSharpAssemblyContext> GetAssemblyContexts(string assemblyFullName)
        {
            lock (_assemblyContexts) {
                Cleanup();

                return _assemblyContexts
                    .Where(a => a.IsAlive && a.FullName == assemblyFullName)
                    .ToArray();
            }
        }

        public IEnumerable<LiveSharpAssemblyContext> GetAllAssemblyContexts()
        {
            lock (_assemblyContexts) {
                Cleanup();

                return _assemblyContexts.ToArray();
            }
        }

        public LiveSharpAssemblyContext GetAssemblyContext(Assembly assembly)
        {
            lock (_assemblyContexts) {
                return _assemblyContexts.FirstOrDefault(a => a.Assembly == assembly);
            }
        }

        // Cleanup should only be called from `locked` methods above
        private void Cleanup()
        {
            var unloadedAssemblies = _assemblyContexts.Where(a => !a.IsAlive).ToArray();

            foreach (var unloadedAssembly in unloadedAssemblies) {
                Logger?.LogMessage("Assembly unloaded: " + unloadedAssembly.FullName);
                _assemblyContexts.Remove(unloadedAssembly);
            }
        }

        public void AddDelegateFieldMapping(Type declaringType, string methodName, string methodIdentifier, Type fieldHost, string fieldName, Type returnType,
            Type[] parameterTypes)
        {
            var assemblyContext = GetAssemblyContext(declaringType.Assembly);

            if (assemblyContext == null)
                assemblyContext = AddAssembly(declaringType.Assembly);
            // if (assemblyContext == null)
            //     throw new InvalidOperationException($"Assembly context not found for {declaringType.Assembly}");

            assemblyContext.AddDelegateFieldMapping(declaringType, methodName, methodIdentifier, fieldHost, fieldName, returnType, parameterTypes);
        }

        public bool GetMethodUpdate(Assembly assembly, string methodIdentifier, out VirtualMethodInfo metadata)
        {
            var assemblyContext = GetAssemblyContext(assembly);

            if (assemblyContext != null)
                return assemblyContext.AllMethods.TryGetValue(methodIdentifier, out metadata);

            metadata = null;
            return false;
        }

        public TypeInfo GetOrCreateVirtualTypeInfo(Type type, bool isAsyncStateMachine = false)
        {
            var assemblyContext = LiveSharpRuntime.AssemblyContextRegistry.GetAssemblyContext(type.Assembly);
            return assemblyContext?.GetOrCreateVirtualTypeInfo(type.FullName, isAsyncStateMachine, type);
        }

        public MethodCallHandler GetInterceptorHandler(Type interceptedType, string interceptedMethodName)
        {
            foreach (var interceptor in _interceptorHandlers) {
                var (declaringType, methodName) = interceptor.Key;
                var interceptorHandler = interceptor.Value;
                var nameMatches = methodName == null || interceptedMethodName == methodName;
                var typeMatches = declaringType.IsAssignableFrom(interceptedType);

                if (nameMatches && typeMatches)
                    return interceptorHandler;
            }

            return null;
        }

        public MethodCallHandler GetInterceptorHandler(DelegateBuilder metadata)
        {
            return GetInterceptorHandler(CompilerHelpers.ResolveVirtualType(metadata.DeclaringType), metadata.Name);
        }

        public void CreateCallInterceptors(Type declaringType, string methodName, MethodCallHandler callHandler,
            Type excludeType)
        {
            _interceptorHandlers[(declaringType, methodName)] = callHandler;

            var assemblyContext = GetAssemblyContext(declaringType.Assembly);
            if (assemblyContext != null) {
                assemblyContext.CreateCallInterceptors(declaringType, methodName, callHandler, excludeType);
            }
            else {
                foreach (var context in GetAllAssemblyContexts())
                    context.CreateCallInterceptors(declaringType, methodName, callHandler, excludeType);
            }
        }
    }
}