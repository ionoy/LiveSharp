using System.Collections.Generic;
using System.Reflection;

namespace LiveSharp
{
    public interface ILiveSharpLoadContext
    {
        Assembly MainAssembly { get; }
        List<Assembly> ReferenceAssemblies { get; }
    }
}