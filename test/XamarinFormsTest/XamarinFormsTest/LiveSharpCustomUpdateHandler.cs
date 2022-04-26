//using LiveSharp.Runtime;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Xamarin.Forms;

namespace XamarinFormsTest
{
    //class LiveSharpCustomUpdateHandler
    //{
    //    private static WeakReference<object> _latestContentPage = new WeakReference<object>(null);
    //    private static ConditionalWeakTable<INotifyPropertyChanged, InvocationInfo> _vmCtorCalls = new ConditionalWeakTable<INotifyPropertyChanged, InvocationInfo>();
    //    public static void HandleCall(object instance, string methodIdentifier, object[] args, Type[] argTypes)
    //    {
    //        if (instance is INotifyPropertyChanged inpc && methodIdentifier.IndexOf(" .ctor ") != -1) {
    //            // Base constructors would cause the same instance to be added without this check
    //            if (!_vmCtorCalls.TryGetValue(inpc, out _))
    //                _vmCtorCalls.Add(inpc, new InvocationInfo(args, argTypes));
    //        }

    //        if (instance is ContentPage && methodIdentifier.EndsWith(" Build "))
    //            _latestContentPage.SetTarget(instance);
    //    }

    //    public static void HandleUpdate(Dictionary<string, IReadOnlyList<object>> updatedMethods)
    //    {
    //        var instances = updatedMethods.SelectMany(kvp => kvp.Value)
    //                                      .Where(i => i != null)
    //                                      .Distinct()
    //                                      .ToArray();

    //        Device.BeginInvokeOnMainThread(() => {
    //            var found = false;
    //            var updatedContexts = new HashSet<Type>();

    //            foreach (var instance in instances) {
    //                try {
    //                    if (instance is INotifyPropertyChanged inpc)
    //                        updatedContexts.Add(inpc.GetType());

    //                    if (CallBuildMethod(instance))
    //                        found = true;
    //                } catch (TargetInvocationException e) {
    //                    var inner = e.InnerException;
    //                    while (inner is TargetInvocationException tie)
    //                        inner = tie.InnerException;
    //                    if (inner != null)
    //                        throw inner;
    //                    throw;
    //                }
    //            }

    //            UpdateViewModels(updatedContexts);

    //            if (!found) {
    //                if (_latestContentPage.TryGetTarget(out var contentPage))
    //                    CallBuildMethod(contentPage);
    //            }
    //        });
    //    }

    //    private static void UpdateViewModels(HashSet<Type> updatedContexts)
    //    {
    //        var children = GetLogicalDescendants(Application.Current);

    //        foreach (var child in children) {
    //            var oldContext = child.BindingContext;
    //            if (oldContext != null) {
    //                var contextType = oldContext.GetType();
    //                var isCurrentlyUpdated = updatedContexts.Contains(contextType);

    //                if (isCurrentlyUpdated && _vmCtorCalls.TryGetValue(child, out var args)) {
    //                    if (oldContext is IDisposable disposable)
    //                        disposable.Dispose();

    //                    var update = LiveSharpRuntime.GetUpdate(oldContext, ".ctor", args.ParameterTypes, args.Arguments);
    //                    if (update != null)
    //                        LiveSharpRuntime.ExecuteVoid(update, oldContext, args.Arguments);

    //                    child.BindingContext = null;
    //                    child.BindingContext = oldContext;
    //                }
    //            }
    //        }
    //    }

    //    private static bool CallBuildMethod(object instance)
    //    {
    //        var buildMethod = GetMethod(instance, "Build", true);

    //        if (buildMethod != null) {
    //            buildMethod.Invoke(instance, null);
    //            return true;
    //        }

    //        return false;
    //    }

    //    private static IEnumerable<Element> GetLogicalDescendants(Element parent)
    //    {
    //        var ec = (IElementController)parent;
    //        foreach (var child in ec.LogicalChildren) {
    //            yield return child;
    //            foreach (var grandChild in GetLogicalDescendants(child))
    //                yield return grandChild;
    //        }
    //    }

    //    private static ConstructorInfo FindConstructor(Type owner, Type[] parameterTypes)
    //    {
    //        var constructors = owner.GetConstructors();

    //        foreach (var ctor in constructors) {
    //            var parameters = ctor.GetParameters();

    //            if (TypesMatch(parameters.Select(p => p.ParameterType).ToArray(), parameterTypes))
    //                return ctor;
    //        }

    //        return null;
    //    }

    //    private static bool TypesMatch(Type[] left, Type[] right)
    //    {
    //        if (left.Length != right.Length)
    //            return false;

    //        for (int i = 0; i < left.Length; i++)
    //            if (left[i] != right[i])
    //                return false;

    //        return true;
    //    }

    //    public static object GetAndCallMethod(object instance, string name, TypeInfo[] parameterTypes, object[] values)
    //    {
    //        var method = GetMethod(instance, name, true, parameterTypes);
    //        if (method != null)
    //            return method.Invoke(instance, values);

    //        throw new InvalidOperationException("Unable to call method " + name + " on type " + instance.GetType() + ". Method not found");
    //    }
    //    public static MethodInfo GetMethod(object instance, string name, bool isInstance, TypeInfo[] parameterTypes = null)
    //    {
    //        var reflectableInstance = instance as IReflectableType;
    //        var typeInfo = reflectableInstance?.GetTypeInfo() ?? instance.GetType().GetTypeInfo();

    //        return GetMethod(typeInfo, "Build", true, parameterTypes);
    //    }

    //    public static MethodInfo GetMethod(TypeInfo type, string name, bool isInstance, TypeInfo[] parameterTypes = null)
    //    {
    //        return GetAllMethods(type).Where(m => m.Name == name)
    //                                  .FirstOrDefault(mi => (isInstance ? !mi.IsStatic : mi.IsStatic) && 
    //                                                        HasMatchingParameterTypes(mi, parameterTypes));
    //    }

    //    public static IEnumerable<MethodInfo> GetAllMethods(TypeInfo type)
    //    {
    //        var typeInfo = type.GetTypeInfo();

    //        while (true) {
    //            foreach (var method in typeInfo.DeclaredMethods)
    //                yield return method;

    //            if (typeInfo.BaseType != null)
    //                typeInfo = typeInfo.BaseType.GetTypeInfo();
    //            else
    //                break;
    //        }
    //    }
    //    private static bool HasMatchingParameterTypes(MethodInfo methodInfo, TypeInfo[] parameterTypes)
    //    {
    //        if (parameterTypes == null)
    //            return true;

    //        var parameters = methodInfo.GetParameters();

    //        if (parameters.Length != parameterTypes.Length)
    //            return false;

    //        for (var i = 0; i < parameterTypes.Length; i++)
    //            if (!parameters[i].ParameterType.IsAssignableFrom(parameterTypes[i]))
    //                return false;

    //        return true;
    //    }

    //    class InvocationInfo
    //    {
    //        public object[] Arguments { get; set; }
    //        public Type[] ParameterTypes { get; set; }

    //        public InvocationInfo(object[] arguments, Type[] parameterTypes)
    //        {
    //            Arguments = arguments;
    //            ParameterTypes = parameterTypes;
    //        }
    //    }
    //}
}
