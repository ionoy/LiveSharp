using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using LiveSharp.Support.XamarinForms;
using LiveSharp.Support.XamarinForms.Updating;
using System.Collections.Concurrent;
using Xamarin.Forms;

namespace LiveSharp
{
    public class XamarinFormsViewHandler : ILiveSharpUpdateHandler
    {
        private readonly WeakReference<ContentPage> _latestContentPage = new WeakReference<ContentPage>(null);
        private readonly ConditionalWeakTable<ContentPage, object[]> _pageCtorArguments = new ConditionalWeakTable<ContentPage, object[]>();
        private readonly ConcurrentDictionary<Type, string> _topLevelConstructors = new ConcurrentDictionary<Type, string>();
        
        private TreeTraversal _treeTraversal;
        private ILiveSharpRuntime _runtime;
        private string _pageHotReloadMethodName;
        private bool _missingHotReloadMethodReported = false;
        private bool _isInitialized;
        private XamarinFormsInspector _inspector;
        private bool _initializingContentPage;

        public void Attach(ILiveSharpRuntime runtime)
        {
            if (!_isInitialized) 
                Initialize(runtime);

            runtime.OnCodeUpdateReceived(HandleUpdate);
            runtime.OnMethodCallIntercepted(typeof(ContentPage), HandleContentPageCall);
            runtime.OnServerConnected(() => _inspector.Render());
        }

        private void Initialize(ILiveSharpRuntime runtime)
        {
            _isInitialized = true;
            _runtime = runtime;
            
            if (runtime.Config.TryGetValue("pageHotReloadMethod", out var methodName))
                _pageHotReloadMethodName = methodName;
            else
                _pageHotReloadMethodName = "Build";
            
            _treeTraversal = new TreeTraversal(_runtime.Logger, _pageCtorArguments, _runtime.GetTypeByFullName("Rg.Plugins.Popup.Services.PopupNavigation"));
            _inspector = new XamarinFormsInspector(runtime);
            
            runtime.Logger.LogMessage("Xamarin.Forms View update handler started");
        }

        private void HandleContentPageCall(string methodIdentifier, object instance, object[] args)
        {
            if (instance is ContentPage contentPage && !_initializingContentPage) {
                if (methodIdentifier.Contains(" .ctor "))
                    _latestContentPage.SetTarget(contentPage);
                
                if (methodIdentifier.Contains(" .ctor ") && args.Length > 0) {
                    if (isTopLevelConstructor(contentPage)) {
                        _pageCtorArguments.Remove(contentPage);
                        _pageCtorArguments.Add(contentPage, args);
                    }
                }
                
                _inspector.SetCurrentContext(contentPage);
            }
            
            bool isTopLevelConstructor(ContentPage page)
            {
                var instanceType = page.GetType();
            
                if (_topLevelConstructors.TryGetValue(instanceType, out var topLevelConstructor))
                    return topLevelConstructor == methodIdentifier;

                _topLevelConstructors[instanceType] = methodIdentifier;
            
                return true;
            }
        }

        private void HandleUpdate(IReadOnlyList<IUpdatedMethod> updatedMethods)
        {
            Device.BeginInvokeOnMainThread(() => {
                try {
                    if (_latestContentPage.TryGetTarget(out var latestContentPage)) {
                        _initializingContentPage = true;
                        
                        if (CallHotReloadMethod(latestContentPage))
                            return;
                        if (TryInitializeLatestContentPage(latestContentPage))
                            return;
                    }

                    _treeTraversal.ReloadRootPage();
                }
                catch (TargetInvocationException e) {
                    var inner = e.InnerException;

                    while (inner is TargetInvocationException tie)
                        inner = tie.InnerException;

                    _runtime.Logger.LogError("Xamarin.Forms update handler failed", inner ?? e);
                }
                finally {
                    
                    _initializingContentPage = false;
                }
            });
        }

        private bool TryInitializeLatestContentPage(ContentPage latestContentPage)
        {
            return _treeTraversal.UpdateControlTreeNode(new TreeTraversal.UpdateContext(latestContentPage.GetType().FullName), latestContentPage);
        }

        private bool CallHotReloadMethod(object instance)
        {
            if (TryCallingRuntimeMethod(instance))
                return true;

            var hotReloadMethod = instance.GetMethod(_pageHotReloadMethodName, true);
            if (hotReloadMethod == null) {
                ReportMissingHotReloadMethod(instance);
                return false;
            }
            
            hotReloadMethod.Invoke(instance, null);
            
            return true;
        }

        private bool TryCallingRuntimeMethod(object instance)
        {
            var args = new object[0];
            var methodUpdate = _runtime.GetMethodUpdate(instance.GetType(), _pageHotReloadMethodName, new Type[0]);
            
            if (methodUpdate != null) {
                _runtime.Logger.LogMessage($"Calling {_pageHotReloadMethodName} method");
                methodUpdate.Invoke(instance, args);
                return true;
            }
            
            return false;
        }

        private void ReportMissingHotReloadMethod(object instance)
        {
            if (!_missingHotReloadMethodReported) {
                _runtime.Logger.LogDebug("Unable to find `" + _pageHotReloadMethodName + "` method on `" + instance?.GetType().FullName + "` to perform hot-reload");
                _missingHotReloadMethodReported = true;
            }
        }

        public void Dispose()
        {
        }
    }
}