using System;
using System.ComponentModel;

namespace LiveSharp.Support.Blazor
{
    public static class BlazorServiceProvider
    {
        public static IServiceProvider ServiceProvider { get; set; }
        public static object GetService(Type serviceType)
        {
            return ServiceProvider?.GetService(serviceType);
        }
    }
}