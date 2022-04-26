using LiveSharp.Runtime.IL;
using System;

namespace LiveSharp.Runtime.Infrastructure
{
    public static class RuntimeExtensions
    {
        public static Type ResolveVirtualType(this Type type)
        {
            return CompilerHelpers.ResolveVirtualType(type);
        }
        
        public static Type ResolveGenericTypeParameter(this Type type)
        {
            if (type is GenericTypeParameter gtp)
                return typeof(object);
            return type;
        }
    }
}