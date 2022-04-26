using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xamarin.Forms;
using Xamarin.Forms.Internals;

namespace LiveSharp.Support.XamarinForms.Updating
{
    internal class TreeTraversal
    {
        private readonly ILiveSharpLogger _logger;
        private readonly ConditionalWeakTable<ContentPage, object[]> _storedCtorArguments;
        private readonly Type _rgPopupType;
        private readonly ElementInitializer _elementInitializer;

        public TreeTraversal(ILiveSharpLogger logger, ConditionalWeakTable<ContentPage, object[]> storedCtorArguments,
            Type rgPopupType)
        {
            _logger = logger;
            _storedCtorArguments = storedCtorArguments;
            _rgPopupType = rgPopupType;
            _elementInitializer = new ElementInitializer(logger, _storedCtorArguments);
        }

        public Element GetElementRoot(Element element)
        {
            var root = element;
            while (root.Parent != null)
                root = root.Parent;
            return root;
        }

        public void ReloadType(Type type)
        {
            UpdateControlTree(new UpdateContext(type.FullName));

            Application.Current.MainPage.Opacity = 0;
            Application.Current.MainPage.FadeTo(1, 350);
        }
        private void UpdateControlTree(UpdateContext ctx)
        {
            FindAndUpdateAllDescendants(ctx, Application.Current, 0);
            TryUpdateRgPopupPages(ctx);

            _logger.LogDebug("Updating controls finished");
            
            if (!ctx.ElementFound) {
                _logger.LogWarning("Updated element not found: " + ctx.TypeName);
                _logger.LogMessage("Reloading current page");
                
                ReloadRootPage();
            }
        }

        public void ReloadRootPage()
        {
            var mainPage = Application.Current.MainPage;

            if (mainPage is MasterDetailPage masterDetailPage)
                mainPage = masterDetailPage.Detail;
            
            var emptyXamlContext = new UpdateContext(mainPage.GetType().FullName);

            ReloadPage(mainPage, emptyXamlContext);

            Application.Current.MainPage.Opacity = 0;
            Application.Current.MainPage.FadeTo(1, 350);
        }

        public void ReloadPage(Page page, UpdateContext ctx)
        {
            if (page == null)
                return;

            var pageType = page.GetType();

            if (pageType.FullName == "FreshMvvm.FreshTabbedNavigationContainer") 
            {
                if (page.GetPropertyValue("Children") is IEnumerable<Page> children) {
                    foreach (var child in children)
                        ReloadPage(child, ctx);
                } else {
                    _logger.LogDebug("FreshTabbedNavigationContainer doesn't contain collection of Children");
                }

                return;
            }

            if (pageType.FullName == "FreshMvvm.FreshTabbedFONavigationContainer") 
            {
                var innerPage = page.GetFieldValue("_innerTabbedPage");
                if (innerPage == null) {
                    _logger.LogWarning("FreshTabbedFONavigationContainer doesn't contain _innerTabbedPage");
                    return;
                }

                if (innerPage.GetPropertyValue("Children") is IEnumerable<Page> children) {
                    foreach (var child in children)
                        ReloadPage(child, ctx);
                } else {
                    _logger.LogWarning("FreshTabbedNavigationContainer doesn't contain collection of Children");
                }

                return;
            }

            if (page.Navigation != null)
            {
                var childPage = page.Navigation.NavigationStack.LastOrDefault();
                if (childPage != null && childPage != page) {
                    ReloadPage(childPage, ctx);
                } else {
                    UpdateControlTreeNode(ctx, page);
                }

                return;
            }

            UpdateControlTreeNode(ctx, page);
        }

        private void TryUpdateRgPopupPages(UpdateContext ctx)
        {
            try {
                if (_rgPopupType == null) {
                    _logger.LogDebug("Can't find Rg.Plugins.Popup.Services.PopupNavigation type");
                    return;
                }
            
                _logger.LogDebug("Updating Rg Popup Pages");

                var popupStackProperty = _rgPopupType.FindProperty("PopupStack");
                if (popupStackProperty == null) {
                    _logger.LogDebug("PopupStack property was not found");
                    return;
                }

                var popupStack = popupStackProperty.GetValue(null) as IEnumerable;
                if (popupStack == null) {
                    _logger.LogDebug("Couldn't cast PopupStack to IEnumerable");
                    return;
                }

                var elements = popupStack.OfType<Element>();
                foreach (var element in elements)
                    FindAndUpdateAllDescendants(ctx, element, 0);

                _logger.LogDebug("Updating Rg Popup Pages finished");
            } catch (Exception e) {
                _logger.LogDebug("RgPopup initialization failed: " + e);
            }
        }

        private FindControlResult FindAndUpdateAllDescendants(UpdateContext ctx, Element element, int depth, Element currentListViewOrItemContainer = null)
        {
            if (ctx.TraversedObjects.Contains(element))
                return FindControlResult.Normal();

            ctx.TraversedObjects.Add(element);
            
            var emptyXamlContext = new UpdateContext(ctx.TypeName);
            var elementType = element.GetType();

            _logger.LogDebug(new string(' ', depth * 2) + elementType.Name);

            if (IsListViewOrItemContainer(element) && currentListViewOrItemContainer == null)
                currentListViewOrItemContainer = (ListView)element;
            
            if (elementType.FullName == ctx.TypeName)
            {
                if (currentListViewOrItemContainer != null && currentListViewOrItemContainer != element)
                {
                    var listViewContainer = FindElementContainer(currentListViewOrItemContainer);
                    if (listViewContainer != null)
                    {
                        UpdateControlTreeNode(emptyXamlContext, listViewContainer);
                        ctx.ElementFound = true;
                        return FindControlResult.JumpToAncestor(listViewContainer);
                    }
                }

                UpdateControlTreeNode(ctx, element);

                return FindControlResult.Normal();
            } 
            
            if (element.Is(ctx.TypeName)) {
                UpdateControlTreeNode(ctx, element);
                ctx.ElementFound = true;
                return FindControlResult.Normal();
            }

            IReadOnlyList<ResourceDictionary> dictionaries;
            var hasMergedDictionary = DictionaryUpdate.GetMergedResourceDictionary(element, out dictionaries);
            if (hasMergedDictionary)
            {
                foreach (var dictionary in dictionaries) {
                    var dictionaryType = dictionary.GetType();
                    if (dictionaryType.FullName == ctx.TypeName) {
                        if (element is VisualElement visualElement)
                            DictionaryUpdate.CreateNewMergedResourceDictionary(visualElement.Resources, dictionaryType, _logger);
                        else if (element is Application application)
                            DictionaryUpdate.CreateNewMergedResourceDictionary(application.Resources, dictionaryType, _logger);

                        // If this is a resource inside a Cell then we should reload ListView container
                        var container = FindElementContainer(currentListViewOrItemContainer ?? element);
                        if (container != null)
                        {
                            UpdateControlTreeNode(emptyXamlContext, container);
                            ctx.ElementFound = true;
                            return FindControlResult.JumpToAncestor(container);
                        }

                        break;
                    }
                }
            }

            if (element is VisualElement)
                TraverseNavigationProxy(ctx, element.GetPropertyValue("NavigationProxy"), depth, currentListViewOrItemContainer);

            var elementController = element as IElementController;
            var logicalChildren = elementController != null ? (IEnumerable<Element>)elementController.LogicalChildren : new Element[0];
            var children = (element is ListView ? GetListViewChildren((ListView)element) : logicalChildren).ToArray();

            foreach (var child in children)
            {
                if (child == null)
                    continue;

                var result = FindAndUpdateAllDescendants(ctx, child, depth + 1, currentListViewOrItemContainer);
                if (result.Type == ResultType.JumpToAncestor && child != result.Object)
                    return result;
            }

            // Sometimes logical children are not populated for Shell types
            // in this case we need to enumerate them manually
            if (children.Length == 0) {
                if (element.Is("Xamarin.Forms.ShellSection")) {
                    var contents = element.GetPropertyValue("Items") as IEnumerable;
                    if (contents != null)
                        foreach (Element content in contents)
                            FindAndUpdateAllDescendants(ctx, content, depth + 1, currentListViewOrItemContainer);
                } else if (element.Is("Xamarin.Forms.ShellContent")) {
                    var elementContent = element.GetPropertyValue("Content") as Element;
                    if (elementContent != null)
                        FindAndUpdateAllDescendants(ctx, elementContent, depth + 1, currentListViewOrItemContainer);
                }
            }
            
            return FindControlResult.Normal();
        }

        private bool IsListViewOrItemContainer(Element element)
        {
            return element is ListView || element.Is("CarouselView.FormsPlugin.Abstractions.CarouselViewControl");
        }

        private void TraverseNavigationProxy(UpdateContext ctx, object navigationProxy, int depth, Element currentListView = null)
        {
            if (navigationProxy == null) {
                _logger.LogDebug("Couldn't find NavigationProxy");
                return;
            }

            var modalStack = navigationProxy.GetPropertyValue("ModalStack") as IEnumerable;
            var navigationStack = navigationProxy.GetPropertyValue("NavigationStack") as IEnumerable;

            if (modalStack != null)
                foreach (var navPage in modalStack.OfType<Page>())
                    FindAndUpdateAllDescendants(ctx, navPage, 0);

            if (navigationStack != null)
                foreach (var navPage in navigationStack.OfType<Page>())
                    FindAndUpdateAllDescendants(ctx, navPage, 0);
        }

        private static IEnumerable<Element> GetListViewChildren(ListView listView)
        {
            var items = listView.GetPropertyValue("TemplatedItems");
            if (items != null)
                return ((IEnumerable)items).OfType<Element>();
            return Enumerable.Empty<Element>();
        }

        private static Element FindElementContainer(Element element)
        {
            if (element.GetType().IsCustomControl())
                return element;

            if (element.Parent != null)
                return FindElementContainer(element.Parent);

            return null;
        }

        public bool UpdateControlTreeNode(UpdateContext ctx, Element element)
        {
            _logger.LogMessage("Updating control node " + ctx.TypeName);

            ctx.ElementFound = true;

            if (element == Application.Current.MainPage && element.Is("Xamarin.Forms.Shell")) {
                _elementInitializer.UpdateShellMainPage(element);
                return true;
            }
            
            ClearElement(element);
            
            return _elementInitializer.InitializeComponent(element);
        }

        public void ClearElement(object el)
        {
            //DebugLogger.WriteLine("ClearElement " + el + " -- " + targetId + " -- " + propertyList);

            ClearChildren(el);

            if (el is Element frameworkElement) {
                if (NameScope.GetNameScope(frameworkElement) is NameScope ns) {
                    try {
                        var names = ns.GetFieldValue("_names") as IDictionary;
                        names?.Clear();
                    }
                    catch (Exception e) {
                        _logger.LogError("Couldn't find _names field on NameScope: ", e);
                    }

                    NameScope.SetNameScope(frameworkElement, new NameScope());
                    // TODO! remove all registered elements
                }
                    
            }

            if (el is VisualElement visualElement) 
                visualElement.Resources?.Clear();
        }

        public void ClearChildren(object rootElement)
        {
            try
            {
                if (rootElement is ContentPage)
                {
                    ((ContentPage)rootElement).Content = null;
                }
                else if (rootElement is ContentView)
                {
                    ((ContentView)rootElement).Content = null;
                }
                else if (rootElement is ScrollView)
                {
                    ((ScrollView)rootElement).Content = null;
                }
                else if (rootElement is ContentPresenter)
                {
                    ((ContentPresenter)rootElement).Content = null;
                }
                else if (rootElement is Frame)
                {
                    ((Frame)rootElement).Content = null;
                }
                else if (rootElement is StackLayout)
                {
                    ((StackLayout)rootElement).Children.Clear();
                }
                else if (rootElement is AbsoluteLayout)
                {
                    ((AbsoluteLayout)rootElement).Children.Clear();
                }
                else if (rootElement is RelativeLayout)
                {
                    ((RelativeLayout)rootElement).Children.Clear();
                }

                if (rootElement is Page)
                {
                    var page = ((Page)rootElement);
                    if (page.ToolbarItems != null)
                        page.ToolbarItems.Clear();
                }

                if (rootElement is Application)
                {
                    var app = (Application)rootElement;
                    app.Resources.Clear();
                }
            } catch (Exception e)
            {
                _logger.LogWarning("Unable to ClearChildren: " + e);
            }
        }
        
        private static HashSet<object> FindRootsOnly(IList<object> affectedElements)
        {
            var roots = new HashSet<object>();

            foreach (var element in affectedElements) {
                var fe = element as Element;
                if (fe != null) {
                    var ancestors = GetAncestors(fe);
                    var affectedAncestor =
                        ancestors.LastOrDefault(ancestor => affectedElements.Any(ae => ae != element && ae == ancestor));

                    roots.Add(affectedAncestor ?? element);
                }
                else {
                    roots.Add(element);
                }
            }

            return roots;
        }

        private static IList<Element> GetAncestors(Element element)
        {
            var result = new List<Element>();
            var parent = element.Parent;

            while (parent != null) {
                result.Add(parent);
                var child = parent;
                parent = child.Parent;
            }

            return result;
        }
        
        public static IEnumerable<Element> GetLogicalDescendants(Element parent)
        {
            var ec = (IElementController) parent;
            foreach (var child in ec.LogicalChildren) {
                yield return child;
                foreach (var grandChild in GetLogicalDescendants(child))
                    yield return grandChild;
            }
        }

        private void ReloadMainPage()
        {
            try {
                Application.Current.MainPage = (Page)Activator.CreateInstance(Application.Current.MainPage.GetType());
            } catch (Exception e) {
                _logger.LogError("Failed to reload MainPage", e);
            }
        }

        struct FindControlResult
        {

            public Element Object;
            public ResultType Type;

            public FindControlResult(ResultType result, Element obj = null)
            {
                Type = result;
                Object = obj;
            }

            public static FindControlResult Normal()
            {
                return new FindControlResult(ResultType.Normal);
            }
            
            public static FindControlResult JumpToAncestor(Element ancestor)
            {
                return new FindControlResult(ResultType.JumpToAncestor, ancestor);
            }
        }

        internal class UpdateContext
        {
            public UpdateContext(string typeName)
            {
                TypeName = typeName;
                TraversedObjects = new HashSet<object>();
            }

            public string TypeName { get; set; }
            public HashSet<object> TraversedObjects { get; set; }
            public bool ElementFound { get; set; }
        }

        public enum ResultType { Normal, JumpToAncestor }
    }
    
    internal class DictionaryUpdate
    {
        static FieldInfo _mergedInstanceField = typeof(ResourceDictionary).GetTypeInfo()
                                                                          .GetDeclaredField("_mergedInstance");
        static PropertyInfo _mergedDictionariesProperty = typeof(ResourceDictionary).GetTypeInfo()
                                                                                    .GetDeclaredProperty("MergedDictionaries");

        public static void CreateNewMergedResourceDictionary(ResourceDictionary resourceDictionary, Type updatedDictionaryType, ILiveSharpLogger logger)
        {
            if (resourceDictionary == null) {
                logger.LogWarning("Can't update merged resource dictionary on a null target");
                return;
            }
            
            if (resourceDictionary.MergedWith == updatedDictionaryType && _mergedInstanceField != null) {
                _mergedInstanceField.SetValue(resourceDictionary, Activator.CreateInstance(updatedDictionaryType));
            } else if (_mergedDictionariesProperty != null) {
                var mergedDictionaries = _mergedDictionariesProperty.GetValue(resourceDictionary) as ObservableCollection<ResourceDictionary>;
                var oldMergedDictionary = mergedDictionaries?.FirstOrDefault(d => d.GetType() == updatedDictionaryType);
                if (oldMergedDictionary != null) {
                    mergedDictionaries.Remove(oldMergedDictionary);
                    mergedDictionaries.Add((ResourceDictionary)Activator.CreateInstance(updatedDictionaryType));
                }
            }
        }

        public static bool GetMergedResourceDictionary(Element child, out IReadOnlyList<ResourceDictionary> dictionaries)
        {
            dictionaries = null;

            if (child is VisualElement visualElement)
                return GetMergedResourceDictionary(visualElement.Resources, out dictionaries);

            if (child is Application app)
                return GetMergedResourceDictionary(app.Resources, out dictionaries);

            return false;
        }

        public static bool GetMergedResourceDictionary(ResourceDictionary resourceDictionary, out IReadOnlyList<ResourceDictionary> mergedDictionaries)
        {
            mergedDictionaries = null;

            if (resourceDictionary != null) {
                
                if (_mergedInstanceField != null) {
                    if (_mergedInstanceField.GetValue(resourceDictionary) is ResourceDictionary mergedResources) {
                        mergedDictionaries = new [] { mergedResources };
                        return true;
                    } 
                }
                
                if (_mergedDictionariesProperty != null) {
                    mergedDictionaries = _mergedDictionariesProperty.GetValue(resourceDictionary) as ObservableCollection<ResourceDictionary>;

                    if (mergedDictionaries != null)
                        return true;
                }
            }

            return false;
        }
    }
}