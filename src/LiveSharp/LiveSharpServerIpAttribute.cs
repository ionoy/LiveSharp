using System;

namespace LiveSharp
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class LiveSharpServerIpAttribute : Attribute
    {
        public LiveSharpServerIpAttribute(string ipAddress)
        {
            
        }
    }
}