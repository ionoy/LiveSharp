using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using LiveSharp.Runtime.Virtual;

namespace LiveSharp.Runtime.Infrastructure
{
    public static class ReflectionExtensions
    {
        public static Type GetConditionalAccessType(this Type type)
        {
            if (!type.IsValueType || type == typeof (void))
                return type;

            if (type.IsNullable())
                return type;

            return typeof(Nullable<>).MakeGenericType(type);
        }

        public static bool IsNullable(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
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
            return GetAndCallMethod(instance, name, new Type[] { typeof(TArg0), typeof(TArg1), typeof(TArg2) }, new object[] { arg0, arg1, arg2 });
        }

        public static object CallMethod<TArg0, TArg1, TArg2, TArg3>(this object instance, string name, TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3)
        {
            return GetAndCallMethod(instance, name, new Type[] { typeof(TArg0), typeof(TArg1), typeof(TArg2), typeof(TArg3) }, new object[] { arg0, arg1, arg2, arg3 });
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

        public static ConstructorInfo[] GetConstructors(this Type type)
        {
            return type.GetTypeInfo()
                       .DeclaredConstructors
                       .ToArray();
        }

        public static ConstructorInfo FindDeclaredConstructor(this Type type, params Type[] parameterTypes)
        {
            return type.GetTypeInfo()
                       .DeclaredConstructors
                       .FirstOrDefault(c => {
                           var parameters = c.GetParameters();
                           if (parameters.Length != parameterTypes.Length)
                               return false;
                           return parameters.Zip(parameterTypes, (pi, t) => pi.ParameterType == t).All(b => b);
                       });
        }

        public static MethodInfo FindDeclaredMethod(this Type type, string methodName, params ParameterInfo[] parameters)
        {
            return type.GetTypeInfo()
                .DeclaredMethods
                .FirstOrDefault(c =>
                {
                    if (c.Name != methodName)
                        return false;
                    var parms = c.GetParameters();
                    if (parms.Length != parameters.Length)
                        return false;
                    return parms.Zip(parameters, (pi, t) => pi.ParameterType == t.ParameterType).All(b => b);
                });
        }

        public static MethodInfo FindDeclaredMethod(this Type type, string methodName, bool ignoreVirtualMethods, params Type[] parameterTypes)
        {
            return type.GetTypeInfo()
                .DeclaredMethods
                .FirstOrDefault(c =>
                {
                    if (ignoreVirtualMethods && c is VirtualMethodInfo)
                        return false;
                    if (c.Name != methodName)
                        return false;
                    
                    var parms = c.GetParameters();
                    if (parms.Length != parameterTypes.Length)
                        return false;
                    
                    return parms.Zip(parameterTypes, (pi, type) => pi.ParameterType == type).All(b => b);
                });
        }

        public static bool IsAssignableFrom(this Type type, Type otherType)
        {
            return type.GetTypeInfo().IsAssignableFrom(otherType.GetTypeInfo());
        }

        public static object GetPropertyValue(this object obj, string propertyName)
        {
            var prop = obj.GetType().GetRuntimeProperties().FirstOrDefault(p => p.Name == propertyName);
            if (prop == null)
                return null;

            return prop.GetValue(obj);
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

        public static IEnumerable<FieldInfo> FindAllFields(this Type type)
        {
            while (type != null) {
                TypeInfo ti = type.GetTypeInfo();
                foreach (var field in ti.DeclaredFields)
                    yield return field;

                type = ti.BaseType;
            }
        }

        public static IEnumerable<EventInfo> FindAllEvents(this Type type)
        {
            while (type != null) {
                TypeInfo ti = type.GetTypeInfo();
                foreach (var evt in ti.DeclaredEvents)
                    yield return evt;

                type = ti.BaseType;
            }
        }

        public static EventInfo FindEvent(this Type type, string eventName)
        {
            return type.FindAllEvents().FirstOrDefault(p => p.Name == eventName);
        }

        public static FieldInfo FindField(this Type type, string fieldName)
        {
            return type.FindAllFields().FirstOrDefault(p => p.Name == fieldName);
        }

        public static PropertyInfo FindProperty(this Type type, string propertyName)
        {
            return type.FindAllProperties().FirstOrDefault(p => p.Name == propertyName);
        }

        public static object GetFieldValue(this object obj, string fieldName)
        {
            var prop = obj.GetType().GetRuntimeFields().FirstOrDefault(p => p.Name == fieldName);
            if (prop == null)
                return null;

            return prop.GetValue(obj);
        }

        public static void SetFieldValue(this object obj, string fieldName, object value)
        {
            var prop = obj.GetType().FindField(fieldName);
            if (prop != null)
                prop.SetValue(obj, value);
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

        public static int GetCustomControlAncestorCount(this Type instance)
        {
            var typeInfo = instance.GetTypeInfo();

            if (typeInfo.BaseType == null)
                return 0;

            var count = 0;
            var baseType = typeInfo.BaseType;

            while (baseType != null) {
                if (IsCustomControl(baseType))
                    count++;

                baseType = baseType.GetTypeInfo().BaseType;
            }

            return count;
        }

        public static bool IsDelegate(this Type instance)
        {
            return instance.IsSubclassOf(typeof(Delegate));
        }

        public static bool IsCustomControl(this Type instance)
        {
            var typeInfo = instance.GetTypeInfo();
            var methods = typeInfo.DeclaredMethods;
            // If there is an InitializeComponent method then it's a user defined container
            return methods.FirstOrDefault(m => m.Name == "InitializeComponent") != null;
        }

        public static bool HasBaseType(this Type type, string baseTypeFullName)
        {
            var baseType = type.GetTypeInfo().BaseType;

            while (baseType != null)
            {
                if (baseType.FullName == baseTypeFullName)
                    return true;
                else
                    baseType = baseType.GetTypeInfo().BaseType;
            }

            return false;
        }

        public static bool HasBaseType(this object obj, string baseTypeFullName)
        {
            return obj.GetType().HasBaseType(baseTypeFullName);
        }
    }
}
