using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LiveSharp.Rewriters
{
    public static class CecilExtensions
    {
        public static bool HasAttribute(this MethodDefinition method, string attributeType)
        {
            return method.CustomAttributes.Any(a => a.AttributeType.Name == attributeType || a.AttributeType.FullName == attributeType);
        }
        
        public static IEnumerable<TypeDefinition> GetBaseTypes(this TypeDefinition type)
        {
            while (type.TryResolveBaseType(out type))
                yield return type;
        }
        
        public static bool TryResolveBaseType(this TypeDefinition type, out TypeDefinition baseType)
        {
            try {
                if (type.BaseType != null) {
                    baseType = type.BaseType.Resolve();
                    return true;
                }
                
                baseType = null;
            } catch {
                baseType = null;
            }
            
            return false;
        }

        public static bool Is(this TypeReference typeReference, string typeName)
        {
            if (typeReference.FullName == typeName)
                return true;
            
            var typeDefinition = typeReference.Resolve();

            if (typeDefinition.HasInterface(typeName))
                return true;

            if (typeDefinition.HasBaseType(typeName))
                return true;

            return false;
        }

        public static bool HasBaseType(this TypeDefinition typeDefinition, string baseTypeFullName)
        {
            return typeDefinition.GetBaseTypes().Any(td => td.FullName == baseTypeFullName);
        }

        public static bool HasInterface(this TypeDefinition typeDefinition, string interfaceFullName)
        {
            if (typeDefinition.HasNonInheritedInterface(interfaceFullName))
                return true;

            return typeDefinition.GetBaseTypes().Any(bt => bt.HasNonInheritedInterface(interfaceFullName));
        }

        public static bool HasNonInheritedInterface(this TypeDefinition typeDefinition, string interfaceFullName)
        {
            return typeDefinition.Interfaces.Any(i => i.InterfaceType.FullName == interfaceFullName);
        }
         
        public static IReadOnlyList<T> GetArguments<T>(this CustomAttribute attribute)
        {
            var result = new List<T>();
            
            foreach (var arg in attribute.ConstructorArguments) {
                var argValue = arg.Value;

                if (argValue is IEnumerable<CustomAttributeArgument> subArgs)
                    result.AddRange(subArgs.Select(ca => ca.Value).OfType<T>());

                if (argValue is T t)
                    result.Add(t);
            }

            return result;
        }

        public static T GetPropertyValue<T>(this CustomAttribute attribute, string propertyName)
        {
            if (attribute == null)
                throw new ArgumentNullException(nameof(attribute));

            var property = attribute.Properties.FirstOrDefault(a => a.Name == propertyName);
            if (property.Name != propertyName)
                throw new Exception($"Attribute property `{property}` not found on `{attribute.AttributeType?.Name}`");

            var argumentValue = property.Argument.Value;
            if (argumentValue is T val)
                return val;

            throw new Exception($"Attribute property `{propertyName}` doesn't match requested type `{typeof(T).FullName}`. Actual type: `{argumentValue?.GetType().FullName}`");
        }

        public static bool Match(this Collection<ParameterDefinition> parms, params Type[] parameterTypes)
        {
            if (parms.Count != parameterTypes.Length)
                return false;

            return parms.Zip(parameterTypes, (parmDefinition, parmType) => parmDefinition.ParameterType.FullName == parmType.FullName)
                        .All(b => b);
        }
        
        public static string GetMethodIdentifier(this MethodReference method, bool includeTypeName = true)
        {
            var type = method.DeclaringType.FullName.Replace('/', '+');
            var methodName = method.Name;
            var parameterTypes = method.Parameters.Select(pd => pd.ParameterType).ToArray();
            var signature = GetMethodSignature(parameterTypes);

            if (includeTypeName)
                return type + " " + methodName + " " + signature;
            
            return methodName + " " + signature;
        }
        
        public static string GetMethodSignature(TypeReference[] parameterTypes)
        {
            var result = "";
            for (int i = 0; i < parameterTypes.Length; i++) {
                result += parameterTypes[i].ToString().Replace("<", "[").Replace(">", "]");
                if (i < parameterTypes.Length - 1)
                    result += " ";
            }
            return result;
        }

        public static bool SameReferenceAs(this TypeReference instance, TypeReference other)
        {
            if (instance.Namespace != other.Namespace)
                return false;

            if (instance.Name != other.Name)
                return false;

            // At this point we can't compare Scopes because Int32 from System.Runtime 
            // and Int32 from netstandard are same types but one is forwarded from another Scope
            // Maybe need a way to find a way to resolve TypeForwardedTo stuff
            return true;
            //return instance.Scope.GetScopeName() == other.Scope.GetScopeName();
        }

        private static string GetScopeName(this IMetadataScope instance)
        {
            return instance switch {
                ModuleDefinition md => md.Assembly.Name.FullName,
                AssemblyNameReference anr => anr.FullName,
                _ => throw new InvalidOperationException($"Unknown IMetadataScope {instance}")
            };
        }

        public static bool IsExtensionMethod(this MethodDefinition method)
        {
            return method.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(ExtensionAttribute).FullName);
        }

        public static MethodDefinition GetOrCreateModuleInitializerMethod(this ModuleDefinition module)
        {
            var moduleType = module.Types.FirstOrDefault(t => t.Name == "<Module>");
            if (moduleType == null) {
                moduleType = new TypeDefinition("", "<Module>",
                    TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
                    TypeAttributes.NotPublic);
                module.Types.Add(moduleType);
            }

            var moduleInitializerMethod = moduleType.Methods.FirstOrDefault(m => m.Name == ".cctor");
            if (moduleInitializerMethod == null) {
                moduleInitializerMethod = new MethodDefinition(".cctor", MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
                moduleType.Methods.Add(moduleInitializerMethod);
            }

            return moduleInitializerMethod;
        }

        public static TypeReference GetTypeNoModifier(this TypeReference type)
        {
            if (type.IsRequiredModifier)
                return type.GetElementType();
            return type;
        }

        /* class A<T1> {
         *  class B<T2> {
         *      void M<T3>() => ...;
         *  }
         * }
         * ===>
         * (T1, T2, T3)
         */
        public static List<GenericParameter> CollectGenericParameters(this MethodDefinition method)
        {
            var methodDeclaringType = method.DeclaringType;
            var allGenericParameters = new List<GenericParameter>();

            while (methodDeclaringType != null) {
                allGenericParameters.AddRange(methodDeclaringType.GenericParameters.Reverse());
                methodDeclaringType = methodDeclaringType.DeclaringType;
            }

            allGenericParameters.Reverse();
            allGenericParameters.AddRange(method.GenericParameters);
            return allGenericParameters;
        }
    }
}
