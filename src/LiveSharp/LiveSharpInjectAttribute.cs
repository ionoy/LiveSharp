using System;
using System.Collections.Generic;
using LiveSharp;

namespace LiveSharp
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class LiveSharpInjectAttribute : Attribute
    {
        public LiveSharpInjectAttribute(string pattern)
        {
            
        }
        
        public LiveSharpInjectAttribute(Type type, string methodName = null, params Type[] parameterTypes)
        {
            
        }
    }
}