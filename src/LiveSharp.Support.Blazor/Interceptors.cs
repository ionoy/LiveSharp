using LiveSharp;
using LiveSharp.Support.Blazor.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// [assembly: LiveSharpRewrite.EnableRewriter("BlazorDiReplace")]

namespace LiveSharp.Support.Blazor
{
    // public static class Interceptors
    // {
    //     [LiveSharpRewrite.InterceptCallsToAny(typeof(Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions))]
    //     public static void ServiceCollectionInterceptor(object instance, string methodIdentifier)
    //     {
    //         BlazorAssemblyUpdateHandler.ServiceCollection = (Microsoft.Extensions.DependencyInjection.IServiceCollection)instance;
    //     }
    //     
    //     [LiveSharpRewrite.InterceptCallsTo("Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder", "AddAttribute", transformArguments: true)]
    //     public static void RenderTreeBuilderAddAttributeInterceptor(object instance, int sequence, string name, object value, out int sequenceOut, out string nameOut, out object valueOut)
    //     {
    //         sequenceOut = sequence;
    //         nameOut = name;
    //         valueOut = value;
    //
    //         var routerOpened = isRouterOpened();
    //
    //         if (BlazorAssemblyUpdateHandler.AssemblyLoadContext == null)
    //             return;
    //         
    //         if (name == "AppAssembly" && routerOpened) {
    //             var originalAssembly = value as Assembly;
    //             if (originalAssembly == null)
    //                 return;
    //             
    //             valueOut = BlazorAssemblyUpdateHandler.AssemblyLoadContext.MainAssembly;
    //         } else if (name == "AdditionalAssemblies" && routerOpened) {
    //             var originalAssemblies = value as IEnumerable<Assembly>;
    //             if (originalAssemblies == null)
    //                 return;
    //             
    //             var updatedAssemblies = BlazorAssemblyUpdateHandler.AssemblyLoadContext.ReferenceAssemblies.ToArray();
    //             valueOut = originalAssemblies.Select(a => tryReplaceWithUpdatedAssembly(a, updatedAssemblies));
    //         }
    //         
    //         Assembly tryReplaceWithUpdatedAssembly(Assembly originalAssembly, Assembly[] updatedAssemblies)
    //         {
    //             var updatedAssembly = updatedAssemblies.FirstOrDefault(a => a.GetName().Name == originalAssembly.GetName().Name);
    //
    //             if (updatedAssembly != null) {
    //                 return updatedAssembly;
    //             }
    //
    //             return originalAssembly;
    //         }
    //         
    //         bool isRouterOpened()
    //         {
    //             if (instance.CallMethod("GetFrames")?.GetFieldValue("Array") is Array frames) {
    //                 if (frames.Length > 0) {
    //                     var renderTreeFrames = frames.Cast<object>().Reverse().ToArray();
    //                     var firstComponent = renderTreeFrames
    //                         .Where(f => f != null)
    //                         .FirstOrDefault(f => IsComponent(f)); // RenderTreeFrameType.Component
    //                     
    //                     if (firstComponent?.GetPropertyValue("ComponentType") is Type componentType)
    //                         return componentType.Is("Microsoft.AspNetCore.Components.Routing.Router");
    //                     
    //                     return false;
    //                 }
    //             }
    //
    //             return false;
    //         }
    //     }
    //
    //     private static bool IsComponent(object f)
    //     {
    //         return f.GetPropertyValue("FrameType") is 4;
    //     }
    // }
}