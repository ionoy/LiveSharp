using LiveSharp.Runtime.Api;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiveSharp
{
    public delegate void MethodCallHandler(string methodIdentifier, object instance, object[] args);
    public delegate void CodeUpdateHandler(IReadOnlyList<IUpdatedMethod> updatedMethods);
    public delegate void ResourceUpdateHandler(string path, string content);
    
    public interface ILiveSharpRuntime
    {
        /// <summary>
        /// Use this to output any diagnostic information to the LiveSharp Logs panel
        /// </summary>
        ILiveSharpLogger Logger { get; }
        
        /// <summary>
        /// Use this to pass text based configuration between components
        /// </summary>
        ILiveSharpConfig Config { get; }
        
        /// <summary>
        /// Currently running project information
        /// </summary>
        ProjectInfo ProjectInfo { get; }
        
        /// <summary>
        /// This is required if you want the built-in Blazor platform hot reload 
        /// </summary>
        void UseDefaultBlazorHandler();
        
        /// <summary>
        /// This is required if you want the built-in Xamarin Forms platform hot reload 
        /// </summary>
        /// <param name="hotReloadMethodName">LiveSharp will invoke this method whenever it receives the code update</param>
        void UseDefaultXamarinFormsHandler(string hotReloadMethodName = "Build");
        
        /// <summary>
        /// This is required if you want the built-in Uno platform hot reload 
        /// </summary>
        /// <param name="hotReloadMethodName">LiveSharp will invoke this method whenever it receives the code update</param>
        void UseDefaultUnoHandler(string hotReloadMethodName = "Build");

        /// <summary>
        /// Register a method call intercept handler
        /// This handler will be invoked whenever a specified method is invoked in your app
        /// </summary>
        /// <param name="declaringType">The type that declares the method you want to intercept</param>
        /// <param name="methodName">Method name</param>
        /// <param name="callHandler">Call handler</param>
        void OnMethodCallIntercepted(Type declaringType, string methodName, MethodCallHandler callHandler);
        void OnMethodCallIntercepted(Type declaringType, string methodName, MethodCallHandler callHandler, Type excludeType);
        
        /// <summary>
        /// Register a method call intercept handler
        /// This handler will be invoked whenever a specified method is invoked in your app
        /// </summary>
        /// <param name="declaringType">The type that declares the method you want to intercept</param>
        /// <param name="callHandler">Call handler</param>
        void OnMethodCallIntercepted(Type declaringType, MethodCallHandler callHandler);
        void OnMethodCallIntercepted(Type declaringType, MethodCallHandler callHandler, Type excludeType);

        /// <summary>
        /// Register a code update handler
        /// This handler will be invoked whenever the code update comes from the server
        /// </summary>
        /// <param name="updateHandler">Code update handler</param>
        void OnCodeUpdateReceived(CodeUpdateHandler updateHandler);
        /// <summary>
        /// Register a resource update handler
        /// This handler will be invoked whenever the resource update comes from the server  
        /// </summary>
        /// <param name="resourceUpdateHandler">Resource update handler</param>
        void OnResourceUpdateReceived(ResourceUpdateHandler resourceUpdateHandler);
        
        /// <summary>
        /// Register an assembly update handler
        /// This handler will be invoked whenever the assembly update comes from the server  
        /// </summary>
        /// <param name="assemblyUpdateHandler">Assembly update handler</param>
        void OnAssemblyUpdateReceived(Action<Assembly> assemblyUpdateHandler);
        
        /// <summary>
        /// Run your code when the LiveSharp server is connected
        /// </summary>
        /// <param name="handler">Action to be executed</param>
        void OnServerConnected(Action handler);
        void OnServerConnected(Action<string> handler);
        /// <summary>
        /// Searches and returns the update for a specific method
        /// This will only work if you have updated a method during the current program run
        /// The object returned can be later used by Invoke/InvokeVoid to invoke the method 
        /// </summary>
        /// <param name="declaringType">Type that declares the method</param>
        /// <param name="methodName">Method name</param>
        /// <param name="parameterTypes">List of all the parameters</param>
        IUpdatedMethod GetMethodUpdate(Type declaringType, string methodName, params Type[] parameterTypes);
        
        /// <summary>
        /// Create or update the diagnostic panel in the LiveSharp Dashboard
        /// </summary>
        /// <param name="panelName">Unique name for your panel</param>
        /// <param name="content">Text or HTML markup</param>
        void UpdateDiagnosticPanel(string panelName, string content);

        /// <summary>
        /// Finds a Type object corresponding to the provided type name
        /// </summary>
        /// <param name="fullName">Full type name without the assembly qualifier</param>
        Type GetTypeByFullName(string fullName);

        void OnAssemblyLoadContextCreated(Action<ILiveSharpLoadContext> assemblyLoadContextHandler);
    }
}