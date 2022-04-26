using System;

namespace LiveSharp
{
    class InstanceInfo
    {
        public int InstanceId { get; }
        public object[] ConstructorArguments { get; }
        public string CtorMethodIdentifier { get; }

        public InstanceInfo(int instanceId, object[] constructorArguments, string ctorMethodIdentifier)
        {
            InstanceId = instanceId;
            ConstructorArguments = constructorArguments;
            CtorMethodIdentifier = ctorMethodIdentifier;
        }
    }
}