using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LiveSharp.Runtime;

namespace LiveSharp.ServerClient
{
    internal static class KnownTypes
    {
        private static readonly Dictionary<string, Type> TypesByFullName = new Dictionary<string, Type>();
        private static readonly object _lock = new object();

        public static Type FindType(string typeName, string assembly = null, bool showErrorIfNotFound = true)
        {
            try {
                if (!string.IsNullOrWhiteSpace(assembly))
                {
                    var assemblyQualifiedName = typeName + ", " + assembly;
                    var type = Type.GetType(assemblyQualifiedName);
                    
                    if (type != null)
                        return type;
                }
            
                EnsureTypesLoaded();

                if (typeName.EndsWith("[]")) {
                    var tempType = FindType(typeName.Substring(0, typeName.Length - 2), assembly);
                    return tempType.MakeArrayType();
                }
            
                lock (_lock) {
                    if (TypesByFullName.TryGetValue(typeName, out var ret))
                        return ret;
                }

                if (showErrorIfNotFound)
                    LiveSharpRuntime.Logger.LogError($"Type not found {typeName} {assembly} failed.");

                return null;
            }
            catch (Exception e) {
                if (showErrorIfNotFound)
                    LiveSharpRuntime.Logger.LogError($"Resolving type {typeName} {assembly} failed", e);
                return null;
            }
        }

        private static void EnsureTypesLoaded()
        {
            lock (_lock) {
                if (TypesByFullName.Count == 0) {
                    var loadedAssemblies = new HashSet<string>();
                    var loadAssemblyLock = new object();

                    AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => {
                        loadAssembly(args.LoadedAssembly);
                    };

                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        if (assembly != null)
                            loadAssembly(assembly);

                    void loadAssembly(Assembly assembly)
                    {
                        try {
                            lock (loadAssemblyLock) {
                                if (assembly == null)
                                    return;

                                if (loadedAssemblies.Contains(assembly.GetName().Name))
                                    return;
                                
                                loadedAssemblies.Add(assembly.GetName().Name);
                            
                                LoadTypesFromAssembly(assembly);

                                foreach (var referencedAssemblyName in assembly.GetReferencedAssemblies()) {
                                    var referencedAssembly = Assembly.Load(referencedAssemblyName);
                                    loadAssembly(referencedAssembly);
                                }
                            }
                        }
                        catch (Exception e) {
                            LiveSharpRuntime.Logger.LogDebug($"loadAssembly failed: {assembly?.FullName}: {e}");
                        }
                    }
                }
            }
        }

        private static void LoadTypesFromAssembly(Assembly assembly)
        {
            LiveSharpRuntime.Logger.LogDebug($"Loading types from assembly {assembly.FullName}");

            try {
                if (assembly.FullName.Contains("Anonymously Hosted DynamicMethods Assembly"))
                    return;
                
                var definedTypes = assembly.DefinedTypes ?? new TypeInfo[0];

                foreach (var type in definedTypes.Where(t => t != null))
                    if (!string.IsNullOrWhiteSpace(type.FullName)) {
                        TypesByFullName[type.FullName] = type.AsType();
                        
                        if (type.FullName == "LiveSharp.<>DelegateCache") {
                            var initializerField = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                .FirstOrDefault();

                            if (initializerField != null) {
                                initializerField.GetValue(null);
                            } else {
                                LiveSharpRuntime.Logger.LogDebug($"Couldn't find initializer field on {type.AssemblyQualifiedName}");
                            }
                        }
                    }
            }
            catch (ReflectionTypeLoadException typeLoadException) {
                try {
                    LiveSharpRuntime.Logger.LogWarning($"ReflectionTypeLoadException {assembly.FullName}: " +
                                                       typeLoadException.Message);
                    var types = typeLoadException.Types ?? new Type[0];
                    foreach (var type in types.Where(t => t != null))
                        if (!string.IsNullOrWhiteSpace(type.FullName))
                            TypesByFullName[type.FullName] = type;
                }
                catch (Exception e) {
                    LiveSharpRuntime.Logger.LogError(
                        $"Failed to load ReflectionTypeLoadException types from {assembly.FullName}: ", e);
                }
            }
            catch (Exception e) {
                LiveSharpRuntime.Logger.LogError($"Failed to load assembly types from {assembly.FullName}: ", e);
            }
        }
    }
}