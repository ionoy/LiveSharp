using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace LaunchPad.Runtime.Web;

public static class LaunchPadExtensions
{
    private static string[] _args;
    public static WebApplicationBuilder StartLaunchPad(this WebApplicationBuilder app, string[] args)
    {
        _args = args;
        
        if (!LaunchPadRuntime.IsRunning) {
            LaunchPadRuntime.UpdateHandler += WebApplicationUpdateHandler;
            LaunchPadRuntime.StartListeningForNewAssemblies(args);
        }
        
        app.Services.AddTransient<IHost, LaunchPadHost>();
        
        var applicationPartManager = (ApplicationPartManager)app.Services.LastOrDefault((d => d.ServiceType == typeof (ApplicationPartManager)))?.ImplementationInstance!;
        var allAssemblyParts = applicationPartManager.ApplicationParts.ToArray();
        var partsToSkip = new[] {typeof(AssemblyPart), typeof(CompiledRazorAssemblyPart)};
        
        applicationPartManager.ApplicationParts.Clear();
        
        PopulateDefaultParts(applicationPartManager, Assembly.GetEntryAssembly()?.GetName()?.Name);

        foreach (var applicationPart in allAssemblyParts) {
            if (!partsToSkip.Contains(applicationPart.GetType()))
                applicationPartManager.ApplicationParts.Add(applicationPart);
        }
        
        return app;
    }
    private static void WebApplicationUpdateHandler(Assembly newAssembly)
    {
        var programType = newAssembly.DefinedTypes.FirstOrDefault(t => t.Name == "Program");
        var mainMethod = programType?.GetMethods(BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault(m => m.Name == "<Main>$");
        
        var homeControllerType = newAssembly.DefinedTypes.FirstOrDefault(t => t.Name == "HomeController");
        var homeControllerMethods = homeControllerType?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).ToArray();
        
        foreach (var controllerMethod in homeControllerMethods)
        {
            Console.WriteLine(controllerMethod.Name);
        }
        
        var result = mainMethod?.Invoke(null, new object[] {_args});
        
        Console.WriteLine("!!!!!Result is " + result);
        
        if (result is Task task)
        {
            task.Wait();
        }
    }

    static void PopulateDefaultParts(ApplicationPartManager applicationPartManager, string entryAssemblyName)
    {
        var applicationPartAssemblies = GetApplicationPartAssemblies(entryAssemblyName);
        var assemblySet = new HashSet<Assembly>();
        
        foreach (var assembly in applicationPartAssemblies)
        {
            if (assemblySet.Add(assembly))
            {
                var applicationPartFactory = ApplicationPartFactory.GetApplicationPartFactory(assembly);
                
                foreach (var applicationPart in applicationPartFactory.GetApplicationParts(assembly))
                    applicationPartManager.ApplicationParts.Add(applicationPart);
            }
        }
    }
    
    private static IEnumerable<Assembly> GetApplicationPartAssemblies(string entryAssemblyName)
    {
        var assemblyLoadContext = LaunchPadRuntime.CurrentAssemblyLoadContext;
        var rootAssembly = assemblyLoadContext.LoadFromAssemblyName(new AssemblyName(entryAssemblyName));
        var second = rootAssembly
            .GetCustomAttributes<ApplicationPartAttribute>()
            .Select(name => assemblyLoadContext.LoadFromAssemblyName(new AssemblyName(name.AssemblyName)))
            .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
            .SelectMany(GetAssemblyClosure);
        
        return GetAssemblyClosure(rootAssembly).Concat(second);
    }

    private static IEnumerable<Assembly> GetAssemblyClosure(Assembly rootAssembly)
    {
        yield return rootAssembly;
        var relatedAssemblies = RelatedAssemblyAttribute.GetRelatedAssemblies(rootAssembly, false);
        
        foreach (var relatedAssembly in relatedAssemblies.OrderBy(assembly2 => assembly2.FullName, StringComparer.Ordinal))
            yield return relatedAssembly;
    }
}