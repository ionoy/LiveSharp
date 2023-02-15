using System.Reflection;
using System.Runtime.Loader;
using LiveSharp;

namespace LaunchPad.Runtime;

public class LaunchPadAssemblyLoadContext : AssemblyLoadContext
{
    private readonly LiveSharpAssemblyUpdate[] _assemblyUpdates;
    public LaunchPadAssemblyLoadContext(params LiveSharpAssemblyUpdate[] assemblyUpdates) : base(true)
    {
        _assemblyUpdates = assemblyUpdates;
    }
    
    protected override Assembly Load(AssemblyName assemblyName)
    {
        if (_assemblyUpdates?.Length == 0)
            return null;
        
        Console.WriteLine("Loading assembly: " + assemblyName.Name);
        
        foreach (var assemblyUpdate in _assemblyUpdates)
        {
            if (assemblyUpdate.Name == assemblyName.Name)
            {
                var assembly = Assembly.Load(assemblyUpdate.AssemblyBuffer, assemblyUpdate.SymbolsBuffer);
                
                Console.WriteLine("Loaded updated assembly: " + assemblyName.Name);
                
                return assembly;
            }
        }
        
        return null;
    }
}