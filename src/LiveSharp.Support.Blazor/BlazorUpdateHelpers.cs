using LiveSharp.Support.Blazor.Infrastructure;
using System;

namespace LiveSharp.Support.Blazor
{
    public static class BlazorUpdateHelpers
    {
        public static object GetRendererFromComponent(object instance)
        {
            var renderHandle = instance.GetFieldValue("_renderHandle", true);
            if (renderHandle == null)
                throw new InvalidOperationException("_renderHandle is null");

            var renderer = renderHandle.GetFieldValue("_renderer", true);
            if (renderer == null)
                throw new InvalidOperationException("_renderer is null");
            return renderer;
        }

        public static object GetServiceFromComponent(object instance, Type serviceType)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            var renderer = GetRendererFromComponent(instance);
            var serviceProvider = (IServiceProvider)renderer.GetFieldValue("_serviceProvider", true);
            
            return serviceProvider.GetService(serviceType);
        }
    }
}