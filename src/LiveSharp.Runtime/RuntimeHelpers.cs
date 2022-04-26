using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.ServerClient;

namespace LiveSharp.Runtime
{
    class RuntimeHelpers
    {   
        public static readonly Lazy<MethodInfo> EventSubscribe = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod("Subscribe", new [] { typeof(object), typeof(EventInfo), typeof(object), typeof(object) }));
        public static readonly Lazy<MethodInfo> EventSubscribeWithDelegate = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod("Subscribe", new [] { typeof(object), typeof(EventInfo), typeof(Delegate) }));
        public static readonly Lazy<MethodInfo> EventUnsubscribe = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod("Unsubscribe", new [] { typeof(object), typeof(EventInfo), typeof(object), typeof(object) }));
        public static readonly Lazy<MethodInfo> EventUnsubscribeWithDelegate = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod("Unsubscribe", new [] { typeof(object), typeof(EventInfo), typeof(Delegate) }));
        public static readonly Lazy<MethodInfo> EventInvoke = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod(nameof(InvokeEvent)));
        public static readonly Lazy<MethodInfo> InvokeEventFieldMethod = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod(nameof(InvokeEventField)));
        public static readonly Lazy<MethodInfo> CreateDelegateMethod = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod("CreateDelegate", false, new [] { typeof(Type), typeof(object), typeof(object) }));
        public static readonly Lazy<MethodInfo> CreateDelegateFromExpressionMethod = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod(nameof(CreateDelegateFromExpression), false, new [] { typeof(Type), typeof(LambdaExpression) }));
        public static readonly Lazy<MethodInfo> CallBaseConstructorMethod = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod(nameof(CallBaseConstructor), false, new [] { typeof(Type), typeof(Type[]), typeof(object), typeof(object[]) }));
        public static readonly Lazy<MethodInfo> CallBaseConstructorWithInfoMethod = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod(nameof(CallBaseConstructor), false, new [] { typeof(ConstructorInfo), typeof(object), typeof(object[]) }));
        public static readonly Lazy<MethodInfo> CallBaseConstructorVirtualMethod = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod(nameof(CallBaseConstructorVirtual), false, new [] { typeof(VirtualInvoker), typeof(object), typeof(object[]) }));
        public static readonly Lazy<MethodInfo> CallBaseMethodVoidMethod = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod(nameof(CallBaseMethodVoid), false, new [] { typeof(MethodInfo), typeof(object), typeof(object[]) }));
        public static readonly Lazy<MethodInfo> CallBaseMethodReturningMethod = new Lazy<MethodInfo>(() => typeof(RuntimeHelpers).GetMethod(nameof(CallBaseMethodReturning), false, new [] { typeof(MethodInfo), typeof(object), typeof(object[]) }));
        
        static readonly Dictionary<Delegate, (WeakReference, object)> DelegateInfos = new Dictionary<Delegate, (WeakReference, object)>();

        internal static bool DelegateEquals(Delegate left, Delegate right)
        {
            if (left == right) return true;
            if (left == null) return false;
            if (right == null) return false;

            if (DelegateInfos.TryGetValue(left, out var t0) && DelegateInfos.TryGetValue(right, out var t1))
                return t0.Item1.Target == t1.Item1.Target && t0.Item2 == t1.Item2;

            return false;
        }

        internal static bool DelegateEquals(Delegate left, object rightTarget, object rightHandler)
        {
            if (left == null) return false;

            if (DelegateInfos.TryGetValue(left, out var t0))
                return t0.Item1.Target == rightTarget && t0.Item2 == rightHandler;

            return false;
        }

        static void A() { }

        internal static Delegate CreateDelegate(Type eventHandlerType, object handlerTarget, object methodInfo)
        {
            Delegate del;
            
            if (methodInfo is MethodInfo mi) {
                del = CreateDelegateFromMethod(eventHandlerType, handlerTarget, mi);
            } else if (methodInfo is LambdaExpression le) {
                del = CreateDelegateFromExpression(eventHandlerType, le);
            } else {
                throw new Exception("Invalid handler type for CreateDelegate: " + methodInfo);
            }

            DelegateInfos[del] = (new WeakReference(handlerTarget), methodInfo);

            return del;
        }

        private static Delegate CreateDelegateFromMethod(Type delegateType, object handlerTarget, MethodInfo methodInfo)
        {
            if (delegateType == typeof(MulticastDelegate)) {
                Debug.WriteLine(delegateType + " is delegate " + delegateType.IsDelegate());
                
                return Delegate.CreateDelegate(delegateType, handlerTarget, methodInfo);
            }

            var invokeMethod = GetInvokeMethod(delegateType);
            var parameters = GetInvokeParameters(invokeMethod);
            var castedParameters = Enumerable.OfType<Expression>(parameters);
            var body = methodInfo.IsStatic
                       ? Expression.Call(methodInfo, castedParameters)
                       : Expression.Call(Expression.Constant(handlerTarget), methodInfo, castedParameters);

            var listener = Expression.Lambda(delegateType, body, parameters);
            var del = listener.Compile();
            return del;
        }

        private static Delegate CreateDelegateFromExpression(Type delegateType, LambdaExpression lambda)
        {
            var invokeMethod = GetInvokeMethod(delegateType);
            var parameters = GetInvokeParameters(invokeMethod);
            var castedParameters = parameters.OfType<Expression>().ToArray();
            
            if (lambda.Body is LambdaExpression innerLambda && innerLambda.Type != invokeMethod.ReturnType) {
                if (invokeMethod.ReturnType.IsDelegate()) {
                    var createInnerDelegate = Expression.Call(CreateDelegateFromExpressionMethod.Value,
                        Expression.Constant(invokeMethod.ReturnType),
                        Expression.Constant(innerLambda));

                    innerLambda = Expression.Lambda(invokeMethod.ReturnType, createInnerDelegate, innerLambda.Parameters);
                    lambda = Expression.Lambda(innerLambda, lambda.Parameters);
                }
            }
            
            var lambdaInvoke = Expression.Invoke(lambda, castedParameters);
            var resultingLambda = Expression.Lambda(delegateType, lambdaInvoke, parameters);

            return resultingLambda.Compile();
        }

        private static ParameterExpression[] GetInvokeParameters(MethodInfo method)
        {
            var parameters = method.GetParameters()
                .Select(p => Expression.Parameter(p.ParameterType, p.Name))
                .ToArray();
            return parameters;
        }

        private static MethodInfo GetInvokeMethod(Type eventHandlerType)
        {
            var invokeMethod = eventHandlerType.GetMethod("Invoke");
            return invokeMethod;
        }

        public static void InvokeEvent(object eventTarget, EventInfo eventInfo, object[] arguments)
        {
            var eventDelegate = (MulticastDelegate)GetEventField(eventInfo).GetValue(eventTarget);
            if (eventDelegate != null)
                foreach (var handler in eventDelegate.GetInvocationList())
                    handler.DynamicInvoke(arguments);
        }
        
        public static void InvokeEventField(object eventTarget, FieldInfo eventField, object[] arguments)
        {
            var eventDelegate = (MulticastDelegate)eventField.GetValue(eventTarget);
            if (eventDelegate != null)
                foreach (var handler in eventDelegate.GetInvocationList())
                    handler.DynamicInvoke(arguments);
        }

        public static void Subscribe(object eventTarget, EventInfo eventInfo, object handlerTarget, object handler)
        {
            var handlerDelegate = RuntimeHelpers.CreateDelegate(eventInfo.EventHandlerType, handlerTarget, handler);

            Subscribe(eventTarget, eventInfo, handlerDelegate);
        }

        public static void Subscribe(object eventTarget, EventInfo eventInfo, Delegate handlerDelegate)
        {
            eventInfo.AddEventHandler(eventTarget, handlerDelegate);
        }

        public static void Unsubscribe(object eventTarget, EventInfo eventInfo, object handlerTarget, object handler)
        {
            Delegate delegateToRemove = null;
            
            var multicastDelegate = (MulticastDelegate)GetEventField(eventInfo).GetValue(eventTarget);
            foreach (var handlerDelegate in multicastDelegate.GetInvocationList())
                if (RuntimeHelpers.DelegateEquals(handlerDelegate, handlerTarget, handler))
                    delegateToRemove = handlerDelegate;

            if (delegateToRemove != null)
                eventInfo.RemoveEventHandler(eventTarget, delegateToRemove);
        }

        public static void Unsubscribe(object eventTarget, EventInfo eventInfo, Delegate handlerDelegate)
        {
            if (FindDelegate(eventTarget, eventInfo, handlerDelegate) is Delegate del)
                eventInfo.RemoveEventHandler(eventTarget, del);
        }
        
        private static Delegate FindDelegate(object eventTarget, EventInfo eventInfo, Delegate handlerDelegate)
        {
            var multicastDelegate = (MulticastDelegate) GetEventField(eventInfo).GetValue(eventTarget);
            foreach (var del in multicastDelegate.GetInvocationList())
                if (RuntimeHelpers.DelegateEquals(del, handlerDelegate))
                    return del;
            return null;
        }

        internal static FieldInfo GetEventField(EventInfo eventInfo)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            return eventInfo.DeclaringType.GetField(eventInfo.Name, bindingFlags);
        }

        public static void CallBaseConstructor(Type baseType, Type[] parameterTypes, object target, object[] arguments)
        {
            var ctor = baseType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                parameterTypes, null);

            if (ctor == null)
                return;

            ctor.Invoke(target, arguments);
        }

        public static void CallBaseConstructor(ConstructorInfo ctor, object target, object[] arguments)
        {
            if (ctor == null) throw new ArgumentNullException(nameof(ctor));
            
            ctor.Invoke(target, arguments);
        }

        public static void CallBaseConstructorVirtual(VirtualInvoker invoker, object target, object[] arguments)
        {
            if (invoker == null) throw new ArgumentNullException(nameof(invoker));

            invoker.InvokeMethodVoid(target, arguments);
        }
        
        public static object CallBaseMethodReturning(MethodInfo methodInfo, object target, object[] arguments)
        {
            var ptr = methodInfo.MethodHandle.GetFunctionPointer();
            var parameters = methodInfo.GetParameters();
            var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
            var typeParameters = parameterTypes.Concat(new[] {methodInfo.ReturnType}).ToArray();
            var func = GetDelegate("System.Func", typeParameters, target, ptr);
            
            return func.DynamicInvoke(arguments);
        }
        
        public static void CallBaseMethodVoid(MethodInfo methodInfo, object target, object[] arguments)
        {
            if (methodInfo == null) throw new ArgumentNullException(nameof(methodInfo));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            
            var ptr = methodInfo.MethodHandle.GetFunctionPointer();
            var parameters = methodInfo.GetParameters();
            var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
            var func = GetDelegate("System.Action", parameterTypes, target, ptr);
            
            func.DynamicInvoke(arguments);
        }

        private static Delegate GetDelegate(string baseName, Type[] typeParameters, object target, IntPtr functionPointer)
        {
            var typeParametersLength = typeParameters.Length;
            var delegateTypeName = typeParametersLength > 0 ? baseName + "`" + typeParametersLength : baseName;
            var delegateType = KnownTypes.FindType(delegateTypeName);
            
            if (delegateType == null)
                throw new Exception($"Can't find delegate type '{delegateTypeName}'");

            if (typeParametersLength > 0)
                delegateType = delegateType.MakeGenericType(typeParameters);

            return (Delegate)Activator.CreateInstance(delegateType, target, functionPointer);
        } 
    }
}