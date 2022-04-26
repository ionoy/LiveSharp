using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LiveSharp.Support.Blazor.Infrastructure;
//using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace LiveSharp.Support.Blazor
{
    public class BlazorUpdateHandler : ILiveSharpUpdateHandler
    {

        public object JsRuntime { get; private set; }
        public WeakReference LatestComponent { get; private set; }
        
        private readonly WeakReferenceList<Type, object> _components = new WeakReferenceList<Type, object>();

        private ILiveSharpRuntime _runtime;
        private BlazorInspector _inspector;
        private ILiveSharpLogger _logger;

        private string _scriptInjectMethodIdentifier;
        private bool _isCssDisabled;
        private bool _isInitialized;
        
        private static object _mostRecentRouter;
        private object[] _services = new object[0];
        private Type _componentBaseType;
        
#if LIVEBLAZOR
        private LiveBlazor.LiveBlazorHandler _liveBlazorHandler = new ();  
#endif

        public void Attach(ILiveSharpRuntime runtime)
        {
            if (!_isInitialized)
                Initialize(runtime);

            _componentBaseType = runtime.GetTypeByFullName("Microsoft.AspNetCore.Components.ComponentBase");

            runtime.OnMethodCallIntercepted(_componentBaseType, "BuildRenderTree", HandleBuildRenderTreeCall);
            runtime.OnCodeUpdateReceived(HandleUpdate);
            runtime.OnResourceUpdateReceived(HandleResourceUpdate);
            runtime.OnServerConnected(() => _inspector.Render());
            runtime.OnAssemblyLoadContextCreated(HandleAssemblyContextUpdate);
            
        #if LIVEBLAZOR
            _liveBlazorHandler.Attach(runtime, this);
        #endif
        }

        public void Initialize(ILiveSharpRuntime runtime)
        {
            _isInitialized = true;
            
            _runtime = runtime;
            _inspector = new BlazorInspector(runtime);
            _logger = runtime.Logger;
            _isCssDisabled = _runtime.Config.TryGetValue("disableBlazorCSS", out var isDisabled) &&
                             string.Equals(isDisabled, "true", StringComparison.InvariantCultureIgnoreCase);

            if (_isCssDisabled)
                runtime.Logger.LogMessage("Blazor CSS handler is disabled");

            runtime.Logger.LogMessage("Blazor update handler started");
        }
        
        void HandleBuildRenderTreeCall(string methodIdentifier, object instance, object[] args)
        {
            try {
                if (instance != null) {
                    _components.Add(instance.GetType(), instance);
                    _inspector.BuildRenderTreeCalled(instance);
                    LatestComponent = new WeakReference(instance);

                    if (!_isCssDisabled) {
                        if (JsRuntime == null || _scriptInjectMethodIdentifier == methodIdentifier) {
                            // _scriptInjectMethodIdentifier == methodIdentifier 
                            // is for when the page is reloaded and we need to inject again
                            InitializeCssHotReload(methodIdentifier, args);
                        }
                    }

                    if (instance.GetType().Name == "App") {
                        BlazorAssemblyUpdateHandler.AppComponent = instance;
                    }
                }
            }
            catch (Exception e) {
                _logger.LogError("HandleBuildRenderTreeCall failed", e);
            }
            
#if LIVEBLAZOR
            _liveBlazorHandler.HandleBuildRenderTreeCall(methodIdentifier, instance, args);
#endif
        }

        private void InitializeCssHotReload(string methodIdentifier, object[] args)
        {
            _runtime.Logger.LogDebug("InitializeCssHotReload: " + methodIdentifier);
            
            try {
                for (int i = 0; i < args.Length; i++) {
                    if (args[i] == null)
                        continue;
                    
                    if (args[i].GetType().Name == "RenderTreeBuilder") {
                        var builder = args[i];

                        builder.CallMethod("OpenElement", 0, "script");
                        builder.CallMethod("AddAttribute", 1, "type", "text/javascript");
                        builder.CallMethod("AddMarkupContent", 2,
                            $@"
window.livesharp = {{
    resourceUpdated: function(resourcePath, content) {{
        var links = document.querySelectorAll('link');
        for (var i in links) links[i].href = links[i].href + ""?"" + Date.now();
    }}
}}");
                        builder.CallMethod("CloseElement");

                        _scriptInjectMethodIdentifier = methodIdentifier;
                        JsRuntime = BlazorUpdateHelpers.GetServiceFromComponent(LatestComponent.Target, _runtime.GetTypeByFullName("Microsoft.JSInterop.IJSRuntime"));
                    }
                }
            }
            catch (Exception e) {
                _logger.LogError("Initializing CSS hot reload failed", e);
            }
        }
        private void HandleUpdate(IReadOnlyList<IUpdatedMethod> updatedMethods)
        {
            try {
                var reloadedTypes = new HashSet<Type>();

                // make sure to handle constructors first to run field initializers before rendering
                foreach (var methodContext in updatedMethods.OrderBy(m => m.MethodIdentifier.Contains(" .ctor ") ? 0 : 1)) {
                    var declaringType = methodContext.DeclaringType;

                    if (declaringType.IsNested &&
                        declaringType.DeclaringType?.Is("Microsoft.AspNetCore.Components.ComponentBase") == true) {
                        declaringType = declaringType.DeclaringType;
                    }

                    if (reloadedTypes.Contains(declaringType))
                        continue;

                    reloadedTypes.Add(declaringType);

                    var components = _components.Get(declaringType);

                    if (components.Count == 0) {
                        var latestComponent = LatestComponent?.Target;
                        if (latestComponent != null && latestComponent.GetType() == declaringType)
                            components = new[] {latestComponent};
                    }

                    if (components.Count == 0 && declaringType.Is(_componentBaseType)) {
                        _logger.LogWarning($"No components found to update {declaringType.FullName}");
                    }
                    
                    foreach (var component in components.Distinct()) {
                        if (component == null)
                            continue;

                        try {
                            // cleanup
                            var isDisposed = component.GetFieldValue("_renderHandle")?.GetFieldValue("_renderer")?.GetFieldValue("_disposed");
                            if (isDisposed is bool isDisposedBool && isDisposedBool) {
                                _components.Remove(declaringType, component);
                                continue;
                            }
                        } 
                        catch (Exception e) {
                            _logger.LogError($"Failed to cleanup component {component}", e);
                        }

                        void callStateHasChanged()
                        {
                            if (methodContext.MethodIdentifier.EndsWith(" .ctor ") &&
                                component.Is(methodContext.DeclaringType.FullName))
                                methodContext.Invoke(component);

                            component.CallMethod("StateHasChanged");
                        }

                        component.CallMethod("InvokeAsync", (Action)callStateHasChanged);
                    }
                }
            }
            catch (Exception e) {
                _runtime.Logger.LogError("BlazorUpdateHandler failed to handle code update", e);
            }
        }

        private void HandleAssemblyContextUpdate(ILiveSharpLoadContext alc)
        {
            //BlazorAssemblyUpdateHandler.HandleAssemblyUpdate(alc, _latestComponent, _runtime, _logger);
        }
        
        private void HandleResourceUpdate(string path, string content)
        {
            if (_isCssDisabled)
                return;

            if (JsRuntime == null) {
                JsRuntime = BlazorUpdateHelpers.GetServiceFromComponent(LatestComponent.Target, _runtime.GetTypeByFullName("Microsoft.JSInterop.IJSRuntime"));
                if (JsRuntime == null) {
                    _logger.LogError("Cannot access JSRuntime to update the CSS file");
                    return;
                }
            }

            if (path?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == false)
                return;

            var method = JsRuntime.GetType().GetMethod("InvokeAsync", true, new[] {typeof(string), typeof(object[])});
            var constructedGenericMethod = method.MakeGenericMethod(typeof(object));

            constructedGenericMethod.Invoke(JsRuntime,
                new object[] {"livesharp.resourceUpdated", new object[] {path, content}});
        }

        public static void RouterUpdated(object router)
        {
            BlazorUpdateHandler._mostRecentRouter = router;
        }

        public void Dispose()
        {
#if LIVEBLAZOR
            _liveBlazorHandler.Dispose();
#endif
        }
    }
}