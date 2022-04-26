using System;

namespace LiveSharp
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class LiveSharpSkipStartAttribute : Attribute
    {
        public LiveSharpSkipStartAttribute() {}
    }
}