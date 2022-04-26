using LiveSharp.Runtime.IL;
using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace LiveSharp.Runtime.Virtual
{
    public class VirtualClr
    {
        public static void Stfld<T>(object instance, T value, int fieldToken)
        {
            var vfi = (VirtualFieldInfo)LiveSharpAssemblyContext.ResolveVirtualMember(fieldToken);
            vfi = ResolveGenericField<T>(vfi);
            
            vfi.SetValue(instance, value);
        }
        public static void Stsfld<T>(T value, int fieldToken)
        {
            var vfi = (VirtualFieldInfo)LiveSharpAssemblyContext.ResolveVirtualMember(fieldToken);
            vfi = ResolveGenericField<T>(vfi);
            
            vfi.SetValue(null, value);
        }

        public static T Ldfld<T>(object instance, int fieldToken)
        {
            var vfi = (VirtualFieldInfo)LiveSharpAssemblyContext.ResolveVirtualMember(fieldToken);
            vfi = ResolveGenericField<T>(vfi);

            return (T)vfi.GetValue(instance);
        }

        public static ref T Ldflda<T>(object instance, int fieldToken)
        {
            var vfi = (VirtualFieldInfo)LiveSharpAssemblyContext.ResolveVirtualMember(fieldToken);
            vfi = ResolveGenericField<T>(vfi);
            
            return ref vfi.GetValueRef<T>(instance);
        }

        public static T Ldsfld<T>(int fieldToken)
        {
            var vfi = (VirtualFieldInfo)LiveSharpAssemblyContext.ResolveVirtualMember(fieldToken);
            vfi = ResolveGenericField<T>(vfi);

            return (T)vfi.GetValue(null);
        }

        public static ref T Ldsflda<T>(int fieldToken)
        {
            var vfi = (VirtualFieldInfo)LiveSharpAssemblyContext.ResolveVirtualMember(fieldToken);
            vfi = ResolveGenericField<T>(vfi);
            
            return ref vfi.GetValueRef<T>(null);
        }

        public static void  Initobj<T>(ref T value) where T : new()
        {
            value = new T();
        }

        public static Delegate ResolveDelegate(int memberToken)
        {
            var vfi = (VirtualMethodInfo)LiveSharpAssemblyContext.ResolveVirtualMember(memberToken);
            return vfi.DelegateBuilder.GetDelegate();
        }

        public static TDelegate ResolveGenericDelegate<TDelegate>(int memberToken) where TDelegate : class
        {
            var vfi = (VirtualMethodInfo)LiveSharpAssemblyContext.ResolveVirtualMember(memberToken);
            return vfi.MakeGenericMethod<TDelegate>().DelegateBuilder.GetDelegate() as TDelegate;
        }

        public static DelegateBuilder ResolveMethodMetadata(int memberToken)
        {
            var vfi = (VirtualMethodInfo)LiveSharpAssemblyContext.ResolveVirtualMember(memberToken);
            return vfi.DelegateBuilder;
        }

        public static T UnwrapRef<T>(object arg)
        {
            return (T)arg;
        }

        // public static TReturn CallOnByRef<TReturn, TInstance>(ref TInstance instance, MethodInfo methodInfo, object[] arguments)
        // {
        //     methodInfo.Invoke()
        // }
        //
        // We need IntPtr for stack consistency after rewriting normal Delegate constructor (check comments in Devirtualizer)
        public static TDelegate CreateDelegate<TDelegate>(object instance, IntPtr methodPtr, DelegateBuilder delegateBuilder) where TDelegate : class
        {
            if (methodPtr != IntPtr.Zero)
                return DelegateFactory<TDelegate>.Build(instance, methodPtr);
            
            return delegateBuilder.CreateWrappedDelegateWithInstance<TDelegate>(instance, typeof(TDelegate));
        }

        public static void TryUpdateGenericDelegate<TDelegate>(int availableVersion, ref int constructedVersion, object methodInfo, ref TDelegate del)
            where TDelegate : class
        {
            if (availableVersion != constructedVersion) {
                var vmi = (VirtualMethodInfo)methodInfo;
                del = vmi.MakeGenericMethod<TDelegate>().DelegateBuilder.GetDelegate() as TDelegate;
                constructedVersion = availableVersion;
            }
        }

        static class DelegateFactory<TDelegate> where TDelegate : class
        {
            static readonly Func<object, IntPtr, TDelegate> Factory = CreateFactory();

            public static TDelegate Build(object obj, IntPtr method) => Factory(obj, method);

            private static Func<object, IntPtr, TDelegate> CreateFactory() 
            {
                var delegateCtor = typeof(TDelegate).GetConstructor(new[] { typeof(object), typeof(IntPtr) });
                var targetParameter = Expression.Parameter(typeof(object));
                var methodParameter = Expression.Parameter(typeof(IntPtr));
                var body = Expression.Block(Expression.New(delegateCtor, targetParameter, methodParameter));
                var lambda = Expression.Lambda<Func<object, IntPtr, TDelegate>>(body, targetParameter, methodParameter);

                return lambda.Compile();
            }
        }

        private static VirtualFieldInfo ResolveGenericField<T>(VirtualFieldInfo vfi)
        {
            var fieldType = vfi.FieldType;

            // is it a generic field?
            if (fieldType.ContainsGenericParameters && fieldType != typeof(T))
                return vfi.MakeGenericField(typeof(T));
            
            return vfi;
        }
    }
}