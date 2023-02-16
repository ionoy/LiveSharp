using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        
        if (LaunchPadRuntime.IsUpdated) {
            var applicationPartManager = new ApplicationPartManager();
            PopulateDefaultParts(applicationPartManager, Assembly.GetEntryAssembly()?.GetName()?.Name);
            app.Services.TryAddSingleton(applicationPartManager);
        }
        
        return app;
    }

    public static WebApplicationBuilder ReplaceServices(this WebApplicationBuilder app)
    {
        var servicesToReplace = new List<ServiceDescriptor>();
        
        foreach (var serviceDescriptor in app.Services) 
        {
            var updatedServiceType = GetUpdatedType(serviceDescriptor.ServiceType);
            var updatedImplementationType = GetUpdatedType(serviceDescriptor.ImplementationType);
        
            if (updatedServiceType == null) {
                Console.WriteLine("Updated service type is null for service type " + serviceDescriptor.ServiceType?.FullName + " in assembly " + serviceDescriptor.ServiceType?.Assembly.FullName + "");
                continue;
            }
            
            if (updatedImplementationType == null) {
                Console.WriteLine("Updated implementation type is null for service type " + serviceDescriptor.ServiceType?.FullName + " in assembly " + serviceDescriptor.ServiceType?.Assembly.FullName + "");
                continue;
            }
            
            if (updatedServiceType != serviceDescriptor.ServiceType || updatedImplementationType != serviceDescriptor.ImplementationType)
                servicesToReplace.Add(ServiceDescriptor.Describe(updatedServiceType, updatedImplementationType, serviceDescriptor.Lifetime));
        }
        
        foreach (var serviceDescriptor in servicesToReplace) {
            app.Services.Replace(serviceDescriptor);
        }

        return app;
    }

    private static Type? GetUpdatedType(Type? type)
    {
        if (type == null)
            return type;
            
        var typeAssembly = type.Assembly;
        var assemblyName = typeAssembly.GetName().Name;
            
        if (assemblyName == null)
            return type;

        if (type.FullName == null) {
            Console.WriteLine("Could not find full name for type " + type.FullName + " in assembly " + typeAssembly.FullName + "");
            return type;
        }
            
        if (LaunchPadRuntime.UpdatedAssemblies.TryGetValue(assemblyName, out var updatedAssembly)) {
            if (typeAssembly != updatedAssembly) {
                var updatedType = updatedAssembly.GetType(type.FullName);

                if (updatedType == null) {
                    Console.WriteLine("Could not find type " + type.FullName + " in updated assembly " + updatedAssembly.FullName);
                    return type;
                }

                return updatedType;
            }
        }
        
        return type;
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
        
        var result = mainMethod?.Invoke(null, new object[] {_args.Append("--launchpadreload").ToArray()});
        
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