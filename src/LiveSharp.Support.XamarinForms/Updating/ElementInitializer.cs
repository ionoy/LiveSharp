using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Xaml.Internals;

namespace LiveSharp.Support.XamarinForms.Updating
{
    internal class ElementInitializer
    {
        private readonly ILiveSharpLogger _logger;
        private readonly ConditionalWeakTable<ContentPage, object[]> _storedCtorArguments;

        private bool _isInitialized;
        private Type _mostRecentUpdateType;

        public ElementInitializer(ILiveSharpLogger logger, ConditionalWeakTable<ContentPage, object[]> storedCtorArguments)
        {
            _logger = logger;
            _storedCtorArguments = storedCtorArguments;
        }

        public bool InitializeComponent(Element element)
        {
            try {
                _logger.LogMessage("InitializeComponent " + element);

                if (element is Application) {
                    var mainPage = Application.Current.MainPage;
                    if (mainPage is NavigationPage page) {
                        InitializeComponent(page.CurrentPage);
                    } else {

                        if (mainPage.Is("Xamarin.Forms.Shell")) {
                            UpdateShellMainPage(mainPage);
                        } else {
                            InitializeComponent(mainPage);
                        }
                    }
                } else {
                    var allFields = element.GetType().GetAllFields().ToArray();

                    CallDisappearing(element, allFields);
                    
                    var args = element is ContentPage cp && _storedCtorArguments.TryGetValue(cp, out var a)
                        ? a : new object[0];
                        
                    if (!TryCallingConstructor(element, args)) {
                        _logger.LogWarning("Constructor initialization failed.");
                        return false;
                    }

                    CallAppearing(element, allFields);
                    RefreshBindingContext(element);
                }

                return true;
            } catch (TargetInvocationException e) {
                SetExceptionContent(e.InnerException);
            } catch (Exception e) {
                SetExceptionContent(e);
            }
            return false;
        }

        public void UpdateShellMainPage(Element element)
        {
            var elementType = element.GetType();
            var ctor = elementType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 0);
            if (ctor == null)
                return;

            var currentItem = element.GetPropertyValue("CurrentItem");
            var currentItemIndex = (int)element.GetPropertyValue("Items")
                .GetAndCallMethod("IndexOf", new[] { currentItem.GetType().GetTypeInfo() }, new[] { currentItem });

            element = Application.Current.MainPage = (Page)Activator.CreateInstance(elementType);

            if (currentItemIndex != -1)
            {
                var items = element.GetPropertyValue("Items") as IEnumerable;
                var itemsArray = items.Cast<object>().ToArray();

                if (currentItemIndex < itemsArray.Length)
                    element.SetPropertyValue("CurrentItem", itemsArray[currentItemIndex]);
            }
        }

        internal void Reset()
        {
            _mostRecentUpdateType = null;
        }
        
        private bool TryCallingConstructor(Element originalElement, object[] args)
        {
            try {
                var elementType = originalElement.GetType();

                // This is used when some element has Custom Control base types
                // We don't want to LoadXaml for these types since there is a bug 
                // with type resolution (base.GetType() vs typeof(CurrentType))
                //_userDefinedBaseTypesCount = elementType.GetCustomControlAncestorCount();

                var newElement = (Element)Activator.CreateInstance(elementType, args);

                // We need this for bindings that depend on Parent
                newElement.Parent = originalElement.Parent;
                
                CopyFields(originalElement, elementType, newElement);
                
                CopyContentProperty(originalElement, newElement);

                TryCopyToolbarItems(originalElement, newElement);

                CopyResourcesAndTriggers(originalElement, newElement);

                newElement.BindingContext = originalElement.BindingContext;
                
                // Copy bindable properties from original object
                try {
                    // We need to copy properties that were possibly changed by user code
                    CopyBindableProperties(originalElement, newElement);
                    //CopyBindableProperties(newElement, originalElement);
                } catch (Exception e) {
                    _logger.LogError("Copying BindableProperties failed", e);
                }

                //try {
                //    var descendants = RuntimeUpdateHandler.GetLogicalDescendants(originalElement);

                //    foreach (var descendant in descendants) {
                //        descendant.CallMethod("ApplyBindings");
                //    }
                //} catch (Exception e) {
                //    DebugLogger.WriteLineInfo("ApplyBindings for descendants failed: " + e.Message);
                //}
                
                return true;
            }
            catch (Exception e) {
                if (e.GetType().Name != "MissingMethodException")
                    SetExceptionContent(e);

                _logger.LogError("Failed to call ctor: ", e);
                return false;
            }
        }

        private void CopyBindableProperties(Element fromElement, Element toElement)
        {
            var propertiesFieldValue = fromElement.GetFieldValue("_properties") as IEnumerable;

            if (propertiesFieldValue == null)
                return;

            var propertiesArray = propertiesFieldValue.OfType<object>().ToArray();

            foreach (var propertyEntry in propertiesArray) {
                if (propertyEntry == null)
                    continue;

                BindableProperty bindableProperty;
                object propertyValue;
                
                if (propertyEntry.GetType().Name.StartsWith("KeyValuePair")) {
                    // This is the new way XF stores properties
                    bindableProperty = propertyEntry.GetPropertyValue("Key") as BindableProperty;
                    var context = propertyEntry.GetPropertyValue("Value");
                    if (context == null)
                        continue;

                    propertyValue = context.GetFieldValue("Value");
                } else {
                    // This is the old way XF stores properties
                    bindableProperty = propertyEntry.GetFieldValue("Property") as BindableProperty;
                    propertyValue = propertyEntry.GetFieldValue("Value");
                }

                if (bindableProperty == null)
                    continue;

                if (bindableProperty.PropertyName == "AutowireViewModel")
                    continue;

                if (bindableProperty.PropertyName == "Register" &&
                    bindableProperty.DeclaringType.FullName == "__livexaml.Runtime")
                    continue;

                //if (bindableProperty.PropertyName == "CurrentItem" &&
                //    bindableProperty.DeclaringType.FullName == "Xamarin.Forms.Shell")
                //    continue;

                toElement.SetBindablePropertyValue(bindableProperty, propertyValue, _logger);
            }
        }

        private static void CopyResourcesAndTriggers(Element element, Element newInstance)
        {
            var visualElement = element as VisualElement;
            var visualNewInstance = newInstance as VisualElement;

            if (visualElement != null && visualNewInstance != null) {
                if (visualElement.Resources != null && visualNewInstance.Resources != null) {
                    visualElement.Resources.Clear();
                    foreach (var kvp in visualNewInstance.Resources)
                        visualElement.Resources[kvp.Key] = kvp.Value;
                }
                
                if (visualElement.Triggers != null && visualNewInstance.Triggers != null) {
                    visualElement.Triggers.Clear();
                    foreach (var triggerBase in visualNewInstance.Triggers)
                        visualElement.Triggers.Add(triggerBase);
                }
            }
        }

        private void CopyFields(Element element, Type elementType, Element newInstance)
        {
            var fields = elementType.GetRuntimeFields();
            foreach (var fld in fields) {
                try {
                    if (!fld.IsLiteral && !fld.IsStatic) {
                        var newValue = fld.GetValue(newInstance);
                        if (newValue != null)
                            fld.SetValue(element, newValue);
                    }
                } catch (Exception e) {
                    _logger.LogError("Couldn't copy field " + fld.Name, e);
                }
            }
        }

        private static void CopyContentProperty(Element element, Element newInstance)
        {
            var elementType = element.GetType().GetTypeInfo();
            var types = new List<TypeInfo>();

            types.Add(elementType);
            
            while (elementType.BaseType != null) {
                elementType = elementType.BaseType.GetTypeInfo();
                types.Add(elementType);
            }

            var handledProperties = new HashSet<string>();

            for (int i = types.Count - 1; i >= 0; i--) {
                var type = types[i].AsType();
                var contentPropertyName = GetContentProperty(type);
                if (contentPropertyName == null)
                    continue;

                if (handledProperties.Contains(contentPropertyName))
                    continue;

                handledProperties.Add(contentPropertyName);

                var contentProperty = type.GetRuntimeProperties()
                                          .FirstOrDefault(p => p.Name == contentPropertyName);
                
                if (contentProperty == null)
                    throw new InvalidOperationException($"Content property {contentPropertyName} not found on {type.FullName}"); 
                
                var contentPropertyValue = contentProperty.GetValue(newInstance);

                CopyContentProperty(element, newInstance, type, contentProperty, contentPropertyValue);
            }
        }

        private static void CopyContentProperty(Element element, Element newInstance, Type elementType, PropertyInfo contentProperty, object contentPropertyValue)
        {
            if (element is Grid) {
                CopyGridChildren((Grid)newInstance, (Grid)element);
            } else if (contentPropertyValue is System.Collections.IEnumerable) {
                var methods = contentPropertyValue.GetType()
                                                  .GetRuntimeMethods();
                var clearMethod = methods.FirstOrDefault(m => m.Name == "Clear");
                var addMethod = methods.FirstOrDefault(m => m.Name == "Add");

                if (clearMethod == null || addMethod == null)
                    return;

                var newInstanceItems = contentProperty.GetValue(newInstance) as System.Collections.IEnumerable;
                if (newInstanceItems == null)
                    return;

                clearMethod.Invoke(contentPropertyValue, new object[0]);
                foreach (var newValue in newInstanceItems.OfType<object>().ToList())
                    addMethod.Invoke(contentPropertyValue, new object[] { newValue });

                var elementMethods = elementType.GetAllDeclaredMethods().ToList();
                var onChildrenChangedMethod = elementMethods.FirstOrDefault(m => m.Name == "OnChildrenChanged");
                if (onChildrenChangedMethod != null) {
                    onChildrenChangedMethod.Invoke(element, new object[] { element, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset) });
                }

                var onPagesChangedMethod = elementMethods.FirstOrDefault(m => m.Name == "OnPagesChanged");
                if (onPagesChangedMethod != null) {
                    onPagesChangedMethod.Invoke(element, new object[] { new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset) });
                }
            } else {
                contentProperty.SetValue(newInstance, null);
                contentProperty.SetValue(element, contentPropertyValue);
            }
        }

        private static void CopyGridChildren(Grid from, Grid to)
        {
            var childrenToCopy = from.Children.ToArray();

            to.Children.Clear();
            from.Children.Clear();

            foreach (var child in childrenToCopy) {
                var row = Grid.GetRow(child);
                var rowSpan = Grid.GetRowSpan(child);
                var column = Grid.GetColumn(child);
                var columnSpan = Grid.GetColumnSpan(child);

                to.Children.Add(child, column, column + columnSpan, row, row + rowSpan);
            }
        }

        private static void TryCopyToolbarItems(object element, object newInstance)
        {
            if (element is Page page && newInstance is Page instance) {
                page.ToolbarItems.Clear();
                foreach (var newItem in instance.ToolbarItems)
                    page.ToolbarItems.Add(newItem);
            }
        }

        private static void CallDisappearing(object element, FieldInfo[] allFields)
        {
            var elementType = element.GetType();
            var disappearingHandlerField = allFields.FirstOrDefault(f => f.Name == "Disappearing");
            var onDisappearingMethod = elementType.GetTypeInfo()
                                                  .DeclaredMethods
                                                  .FirstOrDefault(m => m.Name == "OnDisappearing" && m.GetParameters().Length == 0);
            if (onDisappearingMethod != null)
                onDisappearingMethod.Invoke(element, null);

            if (disappearingHandlerField != null) {
                var disappearingHandler = (EventHandler)disappearingHandlerField.GetValue(element);
                if (disappearingHandler != null)
                    disappearingHandler.Invoke(element, EventArgs.Empty);
            }
        }

        private static void CallAppearing(object element, FieldInfo[] allFields)
        {
            var elementType = element.GetType();
            var appearingHandlerField = allFields.FirstOrDefault(f => f.Name == "Appearing");
            var onAppearingMethod = elementType.GetTypeInfo()
                                               .DeclaredMethods
                                               .FirstOrDefault(m => m.Name == "OnAppearing" && m.GetParameters().Length == 0);

            if (onAppearingMethod != null)
                onAppearingMethod.Invoke(element, null);

            if (appearingHandlerField != null) {
                var appearingHandler = (EventHandler)appearingHandlerField.GetValue(element);
                appearingHandler?.Invoke(element, EventArgs.Empty);
            }
        }

        private void RefreshBindingContext(object element)
        {
            try {
                var bindableObject = element as Xamarin.Forms.BindableObject;

                var context = bindableObject?.BindingContext;
                if (context == null)
                    return;

                bindableObject.BindingContext = null;
                bindableObject.BindingContext = context;
                //var elementType = element.GetType();
                //var methods = elementType.GetRuntimeMethods();
                //foreach (var m in methods) {
                //    DebugLogger.WriteLine(m.Name);
                //    DebugLogger.WriteLine(string.Join(", ", m.GetParameters().Select(p => p.Name)));
                //}
                //var obcc = typeof(Element).GetRuntimeMethod("OnBindingContextChanged", new Type[0]);
                //var onBindingContextChangeMethod = methods.FirstOrDefault(m => m.Name == "OnBindingContextChanged");
                //onBindingContextChangeMethod.Invoke(element, new object[0]);
                //obcc.Invoke(element, new object[0]);
            } catch (Exception e) {
                _logger.LogError("Failed to refresh BindingContext", e);
            }
        }
        
        private void SetExceptionContent(Exception exception)
        {
            var message = exception is TargetInvocationException ? "" : exception.Message;

            _logger.LogError("Error", exception);
            
            while (exception.InnerException != null) {
                message += Environment.NewLine + exception.Message;
                exception = exception.InnerException;
            }

            Application.Current.MainPage.DisplayAlert("LiveSharp error", exception.ToString(), "OK");
        }

        private static string GetContentProperty(Type elementType)
        {
            TypeInfo typeInfo = elementType.GetTypeInfo();
            var cpa = typeInfo.CustomAttributes.FirstOrDefault(a => a.AttributeType == typeof(ContentPropertyAttribute));

            if (cpa != null && cpa.ConstructorArguments.Count > 0) {
                var firstArgument = cpa.ConstructorArguments[0];
                if (firstArgument.ArgumentType == typeof(string))
                    return (string)firstArgument.Value;
            }

            if (typeInfo.BaseType != null)
                return GetContentProperty(typeInfo.BaseType);

            return null;
        }
    }
}