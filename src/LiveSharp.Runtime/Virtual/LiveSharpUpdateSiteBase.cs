using System.Runtime.CompilerServices;

namespace LiveSharp.Runtime.Virtual
{
    public abstract class LiveSharpUpdateSiteBase
    {
        public static readonly ConditionalWeakTable<object, object> ExtensionInstances = new ConditionalWeakTable<object, object>();
        
        public static object GetExtensionInstance(object instance)
        {
            return ExtensionInstances.GetOrCreateValue(instance);
        }
    }
}