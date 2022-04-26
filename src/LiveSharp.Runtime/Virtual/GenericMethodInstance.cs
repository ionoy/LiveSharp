using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiveSharp.Runtime.Virtual
{
    public class GenericMethodInstance
    {
        public List<Type> GenericArguments { get; } = new();
        public Type DeclaringType { get; set; }
        public MethodBase MethodInfo { get; set; }

        public GenericMethodInstance(Type declaringType, MethodBase methodInfo, IEnumerable<Type> genericArguments = null)
        {
            DeclaringType = declaringType;
            MethodInfo = methodInfo;
            GenericArguments.AddRange(genericArguments ?? new Type[0]);
        }
    }
}