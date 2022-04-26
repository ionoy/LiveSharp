using System;

namespace LiveSharp
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class LiveSharpStartAttribute : Attribute
    {
        public LiveSharpStartAttribute(Type type, string methodName, params Type[] parameterTypes)
        {
            
        }
    }
}