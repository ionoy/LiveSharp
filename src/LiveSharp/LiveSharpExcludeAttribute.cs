using System;

namespace LiveSharp
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class LiveSharpExcludeAttribute : Attribute
    {
        public LiveSharpExcludeAttribute(string pattern)
        {
            
        }
        
        public LiveSharpExcludeAttribute(Type type, string methodName = null, params Type[] parameterTypes)
        {
            
        }
    }
}