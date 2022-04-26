using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xamarin.Forms;

namespace LiveSharp.Support.XamarinForms
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
        
        public static ConstructorInfo FindConstructor(this Type owner, string ctorMethodIdentifier)
        {
            var constructors = owner.GetConstructors();

            foreach (var ctor in constructors) {
                var parameters = ctor.GetParameters();
                var parametersString = string.Join(" ", parameters.Select(p => p.ParameterType.FullName));
                var methodIdentifier = $"{owner.FullName} .ctor {parametersString}";
                
                if (methodIdentifier == ctorMethodIdentifier)
                    return ctor;
            }

            return null;
        }

        public static object GetAndCallMethod(this object instance, string name, TypeInfo[] parameterTypes, object[] values)
        {
            var method = GetMethod(instance, name, true, parameterTypes);
            if (method != null)
                return method.Invoke(instance, values);

            throw new InvalidOperationException("Unable to call method " + name + " on type " + instance.GetType() + ". Method not found");
        }
        
        public static MethodInfo GetMethod(this object instance, string name, bool isInstance, TypeInfo[] parameterTypes = null)
        {
            //var reflectableInstance = instance as IReflectableType;
            var typeInfo = instance.GetType().GetTypeInfo();

            return GetMethod(typeInfo, name, isInstance, parameterTypes);
        }

        public static MethodInfo GetMethod(this TypeInfo type, string name, bool isInstance, TypeInfo[] parameterTypes = null)
        {
            return GetAllMethods(type).Where(m => m.Name == name)
                .FirstOrDefault(mi => (isInstance ? !mi.IsStatic : mi.IsStatic) && 
                                      HasMatchingParameterTypes(mi, parameterTypes));
        }

        public static IEnumerable<MethodInfo> GetAllMethods(this TypeInfo type)
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
        
        public static bool Is(this object instance, string typeName)
        {
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

        public static bool IsDeclaredIn(this Type type, string typeName)
        {
            type = type.DeclaringType;

            while (type != null) {
                if (type.Is(typeName))
                    return true;
                
                type = type.DeclaringType;
            }

            return false;
        }

        public static Type GetRootDeclarerOfType(this Type type, string typeName)
        {
            while (type != null) {
                if (type.Is(typeName))
                    return type;
                
                type = type.DeclaringType;
            }

            return null;
        }
        
        public static object GetPropertyValue(this object instance, string propertyName)
        {
            var type = instance.GetType();
            return instance.GetPropertyValue(type, propertyName);
        }
        
        public static object GetPropertyValue(this object instance, Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            
            if (property == null)
                throw new Exception($"Property {propertyName} not found on type {type.FullName}");
            
            return property.GetValue(instance);
        }
        
        private static bool HasMatchingParameterTypes(this MethodInfo methodInfo, TypeInfo[] parameterTypes)
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
        
        public static object GetFieldValue(this object obj, string fieldName)
        {
            var prop = obj.GetType().GetAllFields().FirstOrDefault(p => p.Name == fieldName);
            if (prop == null)
                return null;

            return prop.GetValue(obj);
        }
        
        public static bool IsCustomControl(this Type instance)
        {
            var typeInfo = instance.GetTypeInfo();
            var methods = typeInfo.DeclaredMethods;
            // If there is an InitializeComponent method then it's a user defined container
            return methods.FirstOrDefault(m => m.Name == "InitializeComponent") != null;
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
        
        public static void SetPropertyValue(this object obj, string propertyName, object val)
        {
            var prop = obj.GetType().GetRuntimeProperties().FirstOrDefault(p => p.Name == propertyName);
            if (prop == null)
                throw new Exception("Property " + propertyName + " not found");

            prop.SetValue(obj, val);
        }
        
        public static void SetBindablePropertyValue(this BindableObject element, BindableProperty bindableProperty, object val, ILiveSharpLogger logger)
        {
            try
            {
                element.CallMethod("SetValue", bindableProperty, val, false, false);
            }
            catch (Exception e)
            {
                logger.LogError("Failed to SetValue for property " + bindableProperty.PropertyName, e);

                if (!bindableProperty.IsReadOnly)
                    element.SetValue(bindableProperty, val);
            }
        }
        
        public static object CallMethod<TArg0, TArg1, TArg2, TArg3>(this object instance, string name, TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3)
        {
            return GetAndCallMethod(instance, name, new Type[] { typeof(TArg0), typeof(TArg1), typeof(TArg2), typeof(TArg3) }, new object[] { arg0, arg1, arg2, arg3 });
        }

        public static object GetAndCallMethod(this object instance, string name, Type[] parameterTypes, object[] values)
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

        public static IEnumerable<MethodInfo> GetAllDeclaredMethods(this Type type)
        {
            foreach (var method in type.GetTypeInfo().DeclaredMethods)
                yield return method;

            var baseType = type.GetTypeInfo().BaseType;
            if (baseType == null)
                yield break;

            foreach (var baseTypeMethod in GetAllDeclaredMethods(baseType))
                yield return baseTypeMethod;
        }
        
        public static PropertyInfo FindProperty(this Type type, string propertyName)
        {
            return type.FindAllProperties().FirstOrDefault(p => p.Name == propertyName);
        }
        
        public static IEnumerable<PropertyInfo> FindAllProperties(this Type type)
        {
            while (type != null) {
                TypeInfo ti = type.GetTypeInfo();
                foreach (var property in ti.DeclaredProperties)
                    yield return property;

                type = ti.BaseType;
            }
        }
    }
}