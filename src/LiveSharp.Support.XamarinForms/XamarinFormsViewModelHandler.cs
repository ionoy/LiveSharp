using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using LiveSharp.Support.XamarinForms;
using Xamarin.Forms;

namespace LiveSharp
{
    public class XamarinFormsViewModelHandler : ILiveSharpUpdateHandler
    {
        private readonly ConditionalWeakTable<INotifyPropertyChanged, InstanceInfo> _inpcInfos = new ConditionalWeakTable<INotifyPropertyChanged, InstanceInfo>();
        private ILiveSharpRuntime _runtime;
        private int _uniqueId;
        private string[] _ignoredViewModels = new string[0];
        private bool _isInitialized;

        public void Attach(ILiveSharpRuntime runtime)
        {
            if (!_isInitialized) 
                Initialize(runtime);

            runtime.OnCodeUpdateReceived(HandleUpdate);
            runtime.OnMethodCallIntercepted(typeof(INotifyPropertyChanged), ".ctor", HandleCall, typeof(ContentPage));
        }
        
        public void Initialize(ILiveSharpRuntime runtime)
        {
            _runtime = runtime;
            if (_runtime.Config.TryGetValue("ignoredViewModels", out var viewModels)) {
                if (!string.IsNullOrWhiteSpace(viewModels)) {
                    _ignoredViewModels = viewModels.Split(',');
                }
            }
        }

        public void HandleCall(string methodIdentifier, object instance, object[] args)
        {
            if (instance is INotifyPropertyChanged inpc) {
                var typeName = instance.GetType().FullName;
                
                if (_ignoredViewModels.Any(toIgnore => string.Equals(toIgnore, typeName)))
                    return;
                
                // Base constructors would cause the same instance to be added without this check
                if (!_inpcInfos.TryGetValue(inpc, out _))
                    _inpcInfos.Add(inpc, new InstanceInfo(_uniqueId++, args, methodIdentifier));
            }
        }

        public void HandleUpdate(IReadOnlyList<IUpdatedMethod> updatedMethods)
        {
            var types = updatedMethods.Select(method => method.DeclaringType)
                .Where(t => t != null)
                .Distinct()
                .ToArray();

            Device.BeginInvokeOnMainThread(() => {
                var updatedContexts = new HashSet<Type>();

                try {
                    foreach (var type in types) {
                        if (type.Is(typeof(INotifyPropertyChanged).FullName))
                            updatedContexts.Add(type);
                    }

                    UpdateViewModels(updatedContexts);
                } catch (TargetInvocationException e) {
                    var inner = e.InnerException;
                        
                    while (inner is TargetInvocationException tie)
                        inner = tie.InnerException;

                    _runtime.Logger.LogError("Xamarin.Forms update handler failed", inner ?? e);
                }
            });
        }

        private void UpdateViewModels(HashSet<Type> updatedContexts)
        {
            var children = GetLogicalDescendants(Application.Current).ToArray().Distinct();
            // Sometimes same instance of ViewModel can be attached to different BindingContext
            // We need to reuse the newly created instance in these cases
            // Dictionary is then: oldInstance -> newInstance
            var instanceDecloner = new Dictionary<object, object>();
            
            // ViewModel can be propogated down the tree once it's attached to the parent BindingContext
            // we don't want to replace newly created VM with another same one
            var justConstructed = new HashSet<object>();
            
            foreach (var child in children.Where(c => c != null)) {
                var oldContext = child.BindingContext;
                if (oldContext != null) {
                    var contextType = oldContext.GetType();
                    var isCurrentlyUpdated = updatedContexts.Contains(contextType);
                    
                    if (justConstructed.Contains(oldContext))
                        continue;
                    
                    if (oldContext is INotifyPropertyChanged oldVm && isCurrentlyUpdated) {
                        // Search for new instance corresponding to the old instance in question
                        if (!instanceDecloner.TryGetValue(oldVm, out var newVm))
                        {
                            // We need to create a new VM instance since there wasn't one in the `decloner`

                            if (!_inpcInfos.TryGetValue(oldVm, out var instanceInfo))
                            {
                                // LiveSharp didn't handle the constructor call of this VM
                                continue;
                            }

                            var ctor = contextType.FindConstructor(instanceInfo.CtorMethodIdentifier);
                            if (ctor == null)
                            {
                                _runtime.Logger.LogWarning($"Couldn't find constructor on a ViewModel {contextType.FullName} with {instanceInfo.CtorMethodIdentifier}");
                                continue;
                            }

                            newVm = ctor.Invoke(instanceInfo.ConstructorArguments);

                            // Next time we encounter the same oldVm, we will reuse the newVm
                            instanceDecloner[oldVm] = newVm;
                            // Read explanation above
                            justConstructed.Add(newVm);
                            
                            // Update constructor arguments table
                            _inpcInfos.Remove(oldVm);
                        }

                        child.BindingContext = newVm;
                    }
                }
            }
        }

        private static IEnumerable<Element> GetLogicalDescendants(Element parent)
        {
            if (parent == null)
                yield break;
            
            var ec = (IElementController)parent;
            
            foreach (var child in ec.LogicalChildren) {
                yield return child;
                foreach (var grandChild in GetLogicalDescendants(child))
                    yield return grandChild;
            }
            
            if (ec.LogicalChildren.Count != 0)
                yield break;
            
            var parentType = parent.GetType();
            
            if (parentType.Is("Xamarin.Forms.IShellContentController")) {
                yield return (Element)parent.GetPropertyValue("Xamarin.Forms.IShellContentController.Page");
            } else if (parentType.Is("Xamarin.Forms.ShellItem")) {
                var items = (IEnumerable) parent.GetPropertyValue("Items");
                foreach (var item in items.OfType<Element>()) {
                    yield return item;
                    foreach (var itemChild in GetLogicalDescendants(item)) {
                        yield return itemChild;
                    }
                }
            } else if (parentType.Is("Xamarin.Forms.ShellSection")) {
                var items = (IEnumerable) parent.GetPropertyValue("Items");
                foreach (var item in items.OfType<Element>()) {
                    yield return item;
                    foreach (var itemChild in GetLogicalDescendants(item)) {
                        yield return itemChild;
                    }
                }
            }
        }

        public void Dispose()
        {
        }
    }
}