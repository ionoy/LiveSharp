using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using LiveSharp.Support.Uno.Infrastructure;

namespace LiveSharp.Support.Uno
{
    public class UnoUpdateHandler : ILiveSharpUpdateHandler
    {
        private readonly WeakReferenceList<object, object> _userControls = new WeakReferenceList<object, object>();
        private readonly ConditionalWeakTable<object, Delegate> _callBuildDelegates = new ConditionalWeakTable<object, Delegate>();
        private WeakReference _latestComponent;

        private ILiveSharpRuntime _runtime;
        private ILiveSharpLogger _logger;
        private bool _isInitialized;

        public void Attach(ILiveSharpRuntime runtime)
        {
            if (!_isInitialized)
                Initialize(runtime);
            
            var userControlType = runtime.GetTypeByFullName("Windows.UI.Xaml.Controls.UserControl");
            
            runtime.OnMethodCallIntercepted(userControlType, HandleCall);
            runtime.OnCodeUpdateReceived(HandleUpdate);
        }

        public void Initialize(ILiveSharpRuntime runtime)
        {
            _isInitialized = true;
            _runtime = runtime;
            _logger = runtime.Logger;

            _logger.LogMessage("Uno update handler started");
        }

        public void HandleCall(string methodIdentifier, object instance, object[] args)
        {
            var isUserControl = instance.Is("Windows.UI.Xaml.Controls.UserControl");

            if (isUserControl) {
                if (instance != null) {
                    _userControls.Add(instance.GetType().FullName, instance);
                    
                    if (_latestComponent?.Target != instance)
                        _latestComponent = new WeakReference(instance);
                }
            }
        }
        
        public void HandleUpdate(IReadOnlyList<IUpdatedMethod> updatedMethods)
        {
            var reloadedTypes = new HashSet<string>();

            foreach (var methodContext in updatedMethods) {
                var declaringType = methodContext.DeclaringType;

                if (declaringType.IsNested &&
                    declaringType.DeclaringType?.Is("Windows.UI.Xaml.Controls.UserControl") == true) {
                    declaringType = declaringType.DeclaringType;
                }

                if (reloadedTypes.Contains(declaringType.FullName))
                    continue;

                reloadedTypes.Add(declaringType.FullName);

                var userControls = _userControls.Get(declaringType.FullName);

                if (userControls.Count == 0) {
                    var latestComponent = _latestComponent?.Target;
                    if (latestComponent != null)
                        userControls = new[] {latestComponent};
                }

                foreach (var userControl in userControls.Distinct()) {
                    if (userControl == null)
                        continue;

                    var buildMethod = declaringType.GetMethod("Build", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (buildMethod == null)
                        continue;

                    var del = _callBuildDelegates.GetValue(userControl, _ => {
                        var userControlConstant = Expression.Constant(userControl);
                        var dispatcherProperty = Expression.Property(userControlConstant, "Dispatcher");
                        var dispatchedHandlerDelegateType = _runtime.GetTypeByFullName("Windows.UI.Core.DispatchedHandler");
                        var callBuild = Expression.Call(userControlConstant, "Build", new Type[0]);
                        var dispatchedHandlerDelegate = Expression.Lambda(dispatchedHandlerDelegateType, Expression.Block(callBuild));
                        var priorityArgument = Expression.Convert(Expression.Constant(1), _runtime.GetTypeByFullName("Windows.UI.Core.CoreDispatcherPriority"));
                        var callRunAsync = Expression.Call(dispatcherProperty, "RunAsync", new Type[0], priorityArgument,
                            dispatchedHandlerDelegate);

                        return Expression.Lambda(Expression.Block(callRunAsync)).Compile();
                    });
                    
                    del.DynamicInvoke();
                }
            }
        }

        public void Dispose()
        {
        }
    }
}