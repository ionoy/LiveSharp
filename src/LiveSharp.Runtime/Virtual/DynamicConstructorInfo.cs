using System;

namespace LiveSharp.Runtime
{
    class DynamicConstructorInfo : DynamicMember
    {
        public Type[] ParameterTypes { get; set; }
    }
}