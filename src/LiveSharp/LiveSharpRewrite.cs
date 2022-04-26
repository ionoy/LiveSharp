using System;
using System.Reflection.Emit;

namespace LiveSharp
{

    public class LiveSharpRewrite
    {
        
        /// <summary>
        /// Intercepts calls to the target method
        /// Interceptor method is required to have the same parameters as the target methods
        /// 
        /// Example:
        /// void InterceptedMethod(int i, string s) { ... }
        ///
        /// [LiveSharpRewrite.InterceptCallTo(typeof(MyType), nameof(MyType.InterceptedMethod)]
        /// static void Interceptor(object instance, int i, string s) { ... } 
        /// </summary>
        [AttributeUsage(AttributeTargets.Method)]
        public class InterceptCallsToAttribute : Attribute
        {
            /// <summary>
            /// Constructs InterceptCallTo Attribute
            ///
            /// Example:
            /// void InterceptedMethod(int i, string s) { ... }
            ///
            /// [LiveSharpRewrite.InterceptCallTo(typeof(MyType), nameof(MyType.InterceptedMethod))]
            /// static void Interceptor(object instance, int i, string s) { ... } 
            /// </summary>
            /// <param name="typeName">Name of the type you want to intercept</param>
            /// <param name="methodName">Name of the method you want to intercept</param>
            /// <param name="transformArguments">Set this to true if you want to modify incoming arguments
            /// Example:
            /// void InterceptedMethod(int i, string s) { ... }
            ///
            /// [LiveSharpRewrite.InterceptCallTo(typeof(MyType), nameof(MyType.InterceptedMethod), transformArguments: true)]
            /// static void Interceptor(object instance, int i, string s, out int iOut, out string sOut) { ... }
            /// </param>
            public InterceptCallsToAttribute(string typeName, string methodName, bool transformArguments = false)
            {}
        
            /// <summary>
            /// Constructs InterceptCallTo Attribute
            ///
            /// Example:
            /// void InterceptedMethod(int i, string s) { ... }
            ///
            /// [LiveSharpRewrite.InterceptCallTo(typeof(MyType), nameof(MyType.InterceptedMethod))]
            /// static void Interceptor(object instance, int i, string s) { ... } 
            /// </summary>
            /// <param name="type">type you want to intercept</param>
            /// <param name="methodName">Name of the method you want to intercept</param>
            /// <param name="transformArguments">Set this to true if you want to modify incoming arguments
            /// Example:
            /// void InterceptedMethod(int i, string s) { ... }
            ///
            /// [LiveSharpRewrite.InterceptCallTo(typeof(MyType), nameof(MyType.InterceptedMethod), transformArguments: true)]
            /// static void Interceptor(object instance, int i, string s, out int iOut, out string sOut) { ... }
            /// </param>
            public InterceptCallsToAttribute(Type type, string methodName, bool transformArguments = false)
            {}
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class InterceptCallsToAnyAttribute : Attribute
        {
            /// <summary>
            /// Intercepts calls to any methods on target type
            /// Arguments are not captured to avoid array creation
            /// You can use InterceptCallsTo attribute to capture/tranform arguments on specific methods
            /// 
            /// Example:
            /// [LiveSharpRewrite.InterceptCallsToAny(typeof(MyType))] 
            /// static void InterceptInstanceMethod(object instance, string methodIdentifier) { ... } 
            /// static void InterceptStaticMethod(object instance, string methodIdentifier) { ... } 
            /// </summary>
            public InterceptCallsToAnyAttribute(string typeName) {}
            public InterceptCallsToAnyAttribute(Type type) {}
        }

        [AttributeUsage(AttributeTargets.Method)]
        public class InjectBeforeAttribute : Attribute
        {
            
        }
        
        [AttributeUsage(AttributeTargets.Assembly)]
        public class EnableRewriterAttribute : Attribute
        {
            public EnableRewriterAttribute(string rewriterName) {} 
        }
    }

}