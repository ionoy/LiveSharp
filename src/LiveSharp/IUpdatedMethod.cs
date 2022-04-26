using System;
using System.Collections.Generic;

namespace LiveSharp
{
    public interface IUpdatedMethod
    {
        string MethodIdentifier { get; }
        object MethodMetadata { get; }
        Type DeclaringType { get; }
        object Invoke(object instance, params object[] arguments);
    }
}