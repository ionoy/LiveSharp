using System;
using System.Linq;
using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Virtual;

namespace LiveSharp.Runtime
{
    class UpdatedMethodContext : IUpdatedMethod
    {
        public string MethodIdentifier { get; }
        public object MethodMetadata { get; }
        public Type DeclaringType { get; }

        public UpdatedMethodContext(string methodIdentifier, VirtualMethodInfo methodMetadata, Type declaringType)
        {
            MethodIdentifier = methodIdentifier;
            MethodMetadata = methodMetadata;
            DeclaringType = declaringType;
        }

        public object Invoke(object instance, params object[] arguments)
        {
            return ((VirtualMethodInfo)MethodMetadata).DelegateBuilder.Invoke(instance, arguments);
        }
    }
}