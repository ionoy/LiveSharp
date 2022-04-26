using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiveSharp.Support.Blazor.Infrastructure
{
    public static class ReflectionExtensions
    {
        private static readonly Dictionary<Type, string> Aliases =
            new Dictionary<Type, string>()
            {
                { typeof(byte), "byte" },
                { typeof(sbyte), "sbyte" },
                { typeof(short), "short" },
                { typeof(ushort), "ushort" },
                { typeof(int), "int" },
                { typeof(uint), "uint" },
                { typeof(long), "long" },
                { typeof(ulong), "ulong" },
                { typeof(float), "float" },
                { typeof(double), "double" },
                { typeof(decimal), "decimal" },
                { typeof(object), "object" },
                { typeof(bool), "bool" },
                { typeof(char), "char" },
                { typeof(string), "string" },
                { typeof(void), "void" }
            };
        
        public static bool Is(this Type type, Type baseType)
        {
            return baseType.IsAssignableFrom(type);
        }
        
        public static bool Is(this object instance, string typeName)
        {
            if (instance == null)
                return false;
            
            var type = instance.GetType();
            
            return type.Is(typeName);
        }
        
        public static bool Is(this Type type, string typeName)
        {
            while (type != null) {
                if (type.FullName == typeName)
                    return true;
                
                if (type.GetInterfaces().Any(i => i.FullName == typeName))
                    return true;
                
                type = type.BaseType;
            }
            
            return false;
        }
        
        public static object CallMethod(this object instance, string name)
        {
            return GetAndCallMethod(instance, name, new Type[0], new object[0]);
        }
        
        public static object CallMethod<TArg0>(this object instance, string name, TArg0 arg0)
        {
            return GetAndCallMethod(instance, name, new Type[] { typeof(TArg0) }, new object[] { arg0 });
        }
        
        public static object CallMethod<TArg0, TArg1>(this object instance, string name, TArg0 arg0, TArg1 arg1)
        {
            return GetAndCallMethod(instance, name, new Type[] { typeof(TArg0), typeof(TArg1) }, new object[] { arg0, arg1 });
        }
        
        public static object CallMethod<TArg0, TArg1, TArg2>(this object instance, string name, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        {
            return GetAndCallMethod(instance, name, new [] { typeof(TArg0), typeof(TArg1), typeof(TArg2) }, new object[] { arg0, arg1, arg2 });
        }
        
        private static object GetAndCallMethod(object instance, string name, Type[] parameterTypes, object[] values)
        {
            var method = GetMethod(instance.GetType(), name, true, parameterTypes);
        
            if (method != null)
                return method.Invoke(instance, values);
        
            throw new InvalidOperationException("Unable to call method " + name + " on type " + instance.GetType() + ". Method not found");
        }
        
        public static MethodInfo GetMethod(this Type type, string name, bool isInstance, Type[] parameterTypes = null)
        {
            var method = type.GetAllMethods()
                .Where(m => m.Name == name)
                .FirstOrDefault(mi => (isInstance ? !mi.IsStatic : mi.IsStatic) && HasMatchingParameterTypes(mi, parameterTypes));
        
            if (method == null)
                throw new Exception("Method `" + name + "` on type `" + type.Name + "` not found");
        
            return method;
        }
        
        public static IEnumerable<MethodInfo> GetAllMethods(this Type type)
        {
            var typeInfo = type.GetTypeInfo();
        
            while (true) {
                foreach (var method in typeInfo.DeclaredMethods)
                    yield return method;
                
                if (typeInfo.BaseType != null)
                    typeInfo = typeInfo.BaseType.GetTypeInfo();
                else
                    break;
            }
        }
        
        private static bool HasMatchingParameterTypes(MethodInfo methodInfo, Type[] parameterTypes)
        {
            if (parameterTypes == null)
                return true;
        
            var parameters = methodInfo.GetParameters();
        
            if (parameters.Length != parameterTypes.Length)
                return false;
        
            for (var i = 0; i < parameterTypes.Length; i++)
                if (!parameters[i].ParameterType.IsAssignableFrom(parameterTypes[i]))
                    return false;
        
            return true;
        }

        public static IEnumerable<FieldInfo> GetAllFields(this Type type)
        {
            foreach (var fld in type.GetRuntimeFields())
                yield return fld;

            var baseType = type.GetTypeInfo().BaseType;
            if (baseType != null) {
                foreach (var baseTypeFld in GetAllFields(baseType)) {
                    yield return baseTypeFld;
                }
            }
        }
        
        public static object GetFieldValue(this object obj, string fieldName, bool throwIfNotFound = false)
        {
            var fieldInfo = obj.GetType().GetAllFields().FirstOrDefault(p => p.Name == fieldName);

            if (fieldInfo == null) {
                if (throwIfNotFound) 
                    throw new InvalidOperationException($"{fieldName} not found on {obj}");
                
                return null;
            }

            return fieldInfo.GetValue(obj);
        }
        
        public static object GetPropertyValue(this object obj, string propertyName, bool throwIfNotFound = false)
        {
            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var propertyInfo = obj.GetType().GetProperties(bindingFlags).FirstOrDefault(p => p.Name == propertyName);

            if (propertyInfo == null) {
                if (throwIfNotFound) 
                    throw new InvalidOperationException($"{propertyName} not found on {obj}");
                
                return null;
            }

            return propertyInfo.GetValue(obj);
        }
        
        public static void SetPropertyValue(this object obj, string propertyName, object value, bool throwIfNotFound = false)
        {
            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var propertyInfo = obj.GetType().GetProperties(bindingFlags).FirstOrDefault(p => p.Name == propertyName);

            if (propertyInfo == null) {
                if (throwIfNotFound) 
                    throw new InvalidOperationException($"{propertyName} not found on {obj}");
            } else {
                propertyInfo.SetValue(obj, value);
            }
        }
        
        internal static string GetTypeName(this Type type)
        {
            if (type.IsConstructedGenericType) {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                var genericCountIndex = genericTypeDefinition.Name.IndexOf('`');
                if (genericCountIndex == -1)
                    genericCountIndex = genericTypeDefinition.Name.Length;
                var typeDefinitionName = genericTypeDefinition.Name.Substring(0, genericCountIndex);
                var genericTypeArguments = string.Join(", ", type.GenericTypeArguments.Select(t => GetTypeName(t)));
                return typeDefinitionName + "<" + genericTypeArguments + ">";
            } 
            
            if (type.IsArray) {
                return GetTypeName(type.GetElementType()) + "[]";
            }
            
            if (Aliases.TryGetValue(type, out var alias))
                return alias;
            
            return type.Name;
        }
    }
}