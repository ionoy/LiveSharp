using LiveSharp.Support.Blazor.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiveSharp.Support.Blazor
{
    public class BlazorAssemblyUpdateHandler
    { 
        public static ILiveSharpLoadContext AssemblyLoadContext { get; set; }
        public static IServiceCollection ServiceCollection { get; set; }
        public static object AppComponent { get; set; }

        public static void HandleAssemblyUpdate(ILiveSharpLoadContext assemblyLoadContext, WeakReference latestComponentReference, ILiveSharpRuntime runtime, ILiveSharpLogger logger)
        {
            try {
                AssemblyLoadContext = assemblyLoadContext;

                var latestComponent = latestComponentReference?.Target;
                if (latestComponent != null) {
                    UpdateRouter(logger, latestComponent, assemblyLoadContext);
                    UpdateServiceCollection(assemblyLoadContext, logger);
                    
                    if (AppComponent != null) {
                        AppComponent.CallMethod("InvokeAsync", (Action)(() => AppComponent.CallMethod("StateHasChanged")));
                    } else {
                        logger.LogWarning("AppComponent not initialized for the assembly update");
                    }
                }
            }
            catch (Exception e) {
                logger.LogError("Handling assembly update failed", e);
            }
        }

        private static void NavigateToSameUri(ILiveSharpRuntime runtime, object latestComponent)
        {
            var navigationManager = BlazorUpdateHelpers.GetServiceFromComponent(latestComponent, runtime.GetTypeByFullName("Microsoft.AspNetCore.Components.NavigationManager"));
            if (navigationManager == null)
                throw new Exception("NavigationManager is not found in the service provider");

            var absoluteUri = navigationManager.GetPropertyValue("Uri");

            navigationManager.CallMethod("NavigateTo", (string) absoluteUri, true);
        }

        private static void UpdateServiceCollection(ILiveSharpLoadContext loadContext, ILiveSharpLogger logger)
        {
            try {
                if (ServiceCollection == null) {
                    logger.LogWarning("ServiceCollection not initialized, skipping service provider update");
                    return;
                }

                var allAssemblies = loadContext.ReferenceAssemblies.Append(loadContext.MainAssembly).ToArray();
            
                var newServices = new List<ServiceDescriptor>();
                foreach (var serviceDescriptor in ServiceCollection) {
                    if (serviceDescriptor.Lifetime == ServiceLifetime.Scoped || serviceDescriptor.Lifetime == ServiceLifetime.Singleton) {
                        var serviceType = serviceDescriptor.ServiceType;
                        var newServiceTypeAssembly = allAssemblies.FirstOrDefault(a => serviceType?.Assembly.GetName().Name == a.GetName().Name);
                        var newImplementationTypeAssembly = allAssemblies.FirstOrDefault(a => serviceDescriptor.ImplementationType?.Assembly.GetName().Name == a.GetName().Name);

                        if (newServiceTypeAssembly != null || newImplementationTypeAssembly != null) {
                            // if found a service in current assembly, try add it to ServiceCollection
                            if (serviceDescriptor.ImplementationFactory == null && serviceDescriptor.ImplementationInstance == null) {
                                var serviceTypeFullName = serviceType?.FullName;
                                var newServiceType = newServiceTypeAssembly != null && serviceTypeFullName != null
                                    ? newServiceTypeAssembly.GetType(serviceTypeFullName)
                                    : serviceType;

                                var implementationTypeFullName = serviceDescriptor.ImplementationType?.FullName;
                                var newImplementationType = newImplementationTypeAssembly != null && implementationTypeFullName != null
                                    ? newImplementationTypeAssembly.GetType(implementationTypeFullName)
                                    : serviceDescriptor.ImplementationType;

                                newServices.Add(new ServiceDescriptor(newServiceType, newImplementationType, serviceDescriptor.Lifetime));
                            }
                            else {
                                logger.LogError("Services initialized by factory or a singleton method should be in a static method with [LiveSharpOnAssemblyUpdate] attribute defined");
                                return;
                            }
                        }
                    }
                }
            
                foreach (var newService in newServices) {
                    ServiceCollection.Add(newService);
                }

                var factory = new DefaultServiceProviderFactory();
            
                BlazorServiceProvider.ServiceProvider = factory.CreateServiceProvider(ServiceCollection);
            }
            catch (Exception e) {
                logger.LogError("Service provider update failed", e);
            }
        }

        private static void UpdateRouter(ILiveSharpLogger logger, object latestComponent, ILiveSharpLoadContext loadContext)
        {
            try {
                var componentStateDictionary = BlazorUpdateHelpers.GetRendererFromComponent(latestComponent).GetFieldValue("_componentStateById") as IDictionary;
                
                Console.WriteLine($"_componentStateById = {componentStateDictionary}");
                
                if (componentStateDictionary != null) {
                    var router = componentStateDictionary
                        .Values
                        .Cast<object>()
                        .Select(c => c.GetPropertyValue("Component"))
                        .Where(c => c != null)
                        .FirstOrDefault(c => c.Is("Microsoft.AspNetCore.Components.Routing.Router"));

                    if (router != null) {
                        var appAssembly = router.GetPropertyValue("AppAssembly") as Assembly;
                        if (appAssembly != null && appAssembly.GetName().Name == loadContext.MainAssembly.GetName().Name) {
                            router.SetPropertyValue("AppAssembly", loadContext.MainAssembly);
                        }
                        else {
                            if (router.GetPropertyValue("AdditionalAssemblies") is IEnumerable<Assembly> additionalAssemblies) {
                                var additionalAssembliesArray = additionalAssemblies.ToArray();
                                var updatedReferenceAssemblies = loadContext.ReferenceAssemblies;
                                
                                for (int i = 0; i < additionalAssembliesArray.Length; i++) {
                                    var originalAssembly = additionalAssembliesArray[i];
                                    
                                    var updatedReferenceAssembly = updatedReferenceAssemblies.FirstOrDefault(ra => ra.GetName().Name == originalAssembly.GetName().Name);
                                    if (updatedReferenceAssembly != null) {
                                        additionalAssembliesArray[i] = updatedReferenceAssembly;
                                        router.SetPropertyValue("AdditionalAssemblies", additionalAssembliesArray);
                                        break;
                                    }
                                }
                            }
                            else {
                                router.SetPropertyValue("AdditionalAssemblies", loadContext.ReferenceAssemblies.ToArray());
                            }
                        }

                        router.CallMethod("RefreshRouteTable");
                    }
                    else {
                        logger.LogWarning("Router not found for RefreshRouteTable");
                    }
                }
            }
            catch (Exception e) {
                logger.LogWarning("RefreshRouteTable failed: " + e.Message);
            }
        }
    }
}