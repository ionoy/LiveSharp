using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Virtual;

namespace LiveSharp.Runtime.IL
{
    public class DelegateBuilder
    {
        public string Name => MethodInfo.Name;
        public string MethodIdentifier => MethodInfo.MethodIdentifier;
        public XElement MethodElement { get; }
        public ParameterMetadata[] Parameters => MethodInfo.Parameters;
        public bool IsStatic => MethodInfo.IsStatic;
        public Type DelegateType { get; }
        public Type DeclaringType => MethodInfo.DeclaringType;

        public static string DelegateFieldName = nameof(_delegate);

        private readonly LiveSharpAssemblyContext _assemblyContext;

        public VirtualMethodInfo MethodInfo { get; }

        private readonly ConditionalWeakTable<object, object> _wrappedDelegateCache = new();
        private readonly ILogger _logger;
        private Delegate _delegate;
        // we store original delegate here because _delegate may contain interceptor
        private Delegate _pureDelegate;
        private object _compiler;
        private ILiveSharpRuntimeExt _runtimeExtensions => LiveSharpRuntime.RuntimeExtensions;

        public DelegateBuilder(XElement methodElement, LiveSharpAssemblyContext assemblyContext, VirtualMethodInfo virtualMethodInfo, Type delegateType, ILogger logger)
        {
            MethodElement = methodElement;
            DelegateType = delegateType;

            _assemblyContext = assemblyContext;
            MethodInfo = virtualMethodInfo;
            _logger = logger;
            //_instructions = DebuggingIlRewriter.AddDebuggingHandlers(_instructions, this);
        }

        public Delegate GetDelegate() => _delegate ?? InitializeDelegate();

        public Delegate InitializeDelegate()
        {
            _pureDelegate = _delegate = CreateDelegate(DelegateType);

            if (_assemblyContext.Registry.GetInterceptorHandler(this) is var interceptor && interceptor != null)
                SetInterceptor(interceptor);

            _assemblyContext.TryUpdateDelegateField(this, _delegate);

            return _delegate;
        }

        public void SetInterceptor(MethodCallHandler interceptor)
        {
            var parameters = Parameters.Select(p => Expression.Parameter(CompilerHelpers.ResolveVirtualType(p.ParameterType), p.ParameterName)).ToList();
            if (!IsStatic)
                parameters.Insert(0, Expression.Parameter(typeof(object), "this"));

            var instanceArgument = IsStatic ? (Expression)Expression.Constant(null) : parameters[0];
            var parameterExpressions = IsStatic ? parameters : parameters.Skip(1);

            var argumentArrayArgument = Expression.NewArrayInit(typeof(object), parameterExpressions.Select(p => Expression.Convert(p, typeof(object))));
            var interceptorArguments = new[] {
                Expression.Constant(MethodIdentifier),
                instanceArgument,
                argumentArrayArgument
            };

            var lambda = Expression.Lambda(DelegateType,
                Expression.Block(
                    Expression.Invoke(Expression.Constant(interceptor), interceptorArguments),
                    Expression.Invoke(Expression.Constant(_pureDelegate), parameters)),
                parameters);

            _delegate = lambda.Compile();
        }

        public static Delegate CreateInterceptorOnly(Type delegateType, string methodIdentifier, MethodCallHandler interceptor, DelegateSignature delegateSignature)
        {
            var parameters = delegateSignature
                .ParameterTypes
                .Select(type => Expression.Parameter(CompilerHelpers.ResolveVirtualType(type)))
                .ToList();

            var isStatic = delegateType.Name.EndsWith("$s");
            if (!isStatic)
                parameters.Insert(0, Expression.Parameter(typeof(object), "this"));

            var instanceArgument = isStatic ? (Expression)Expression.Constant(null) : parameters[0];
            var arrayElements = isStatic ? parameters : parameters.Skip(1);
            var argumentArrayArgument = Expression.NewArrayInit(typeof(object), arrayElements.Select(e => Expression.Convert(e, typeof(object))));
            var interceptorArguments = new[] {
                Expression.Constant(methodIdentifier),
                instanceArgument,
                argumentArrayArgument
            };

            var body = Expression.Block(
                Expression.Invoke(Expression.Constant(interceptor), interceptorArguments),
                delegateSignature.ReturnType == typeof(void)
                    ? Expression.Empty()
                    : Expression.Default(delegateSignature.ReturnType)
                );

            var lambda = Expression.Lambda(delegateType, body, parameters);

            return lambda.Compile();
        }

        public TDelegate CreateWrappedDelegateWithInstance<TDelegate>(object instance, Type delegateType)
        {
            if (_wrappedDelegateCache.TryGetValue(instance, out var @delegate))
                return (TDelegate)@delegate;

            var parameters = Parameters.Select(p => Expression.Parameter(CompilerHelpers.ResolveVirtualType(p.ParameterType), p.ParameterName)).ToList();
            var innerDelegateArguments = new Expression[] {Expression.Constant(instance)}.Concat(parameters).ToArray();
            var lambda = Expression.Lambda<TDelegate>(Expression.Invoke(Expression.Constant(_delegate), innerDelegateArguments), parameters);

            var newDelegate = lambda.Compile();

            _wrappedDelegateCache.Add(instance, newDelegate);

            return newDelegate;
        }

        public Delegate CreateDelegate(Type delegateType = null)
        {
            try {

                // if (_documentMetadata.DebuggingEnabled) {
                //     var rewriter = new DebuggingIlRewriter(_instructions, this);
                //     _instructionsWithDebugging = rewriter.GetInstructionsWithDebugging();
                //     var compiler = new IlExpressionCompiler(_instructionsWithDebugging, this, _logger);
                //     return compiler.GetDelegate(false, delegateType);
                // }
                // else {

                // if (_virtualMethodInfo.IsGeneric)
                //     throw new InvalidOperationException("Cannot create a delegate from a generic method");

                var methodBody = MethodInfo.GetMethodBody();
                if (_runtimeExtensions?.IsDynamicMethodSupported() == true)
                    return _runtimeExtensions.GetDelegate(this, methodBody, methodBody.Instructions, delegateType, _logger, out _compiler);

                var compiler = new IlExpressionCompiler(MethodInfo, _logger);
                _compiler = compiler;
                return compiler.GetDelegate(methodBody, false, delegateType);

            } catch (Exception e) {
                _logger.LogError("IL Compilation failed: " + Environment.NewLine + MethodElement, e);
                throw new IlCompilationException(e, this);
            }
        }


        public object Invoke(object instance, params object[] arguments)
        {
            if (IsStatic)
                return _delegate.DynamicInvoke(arguments);

            return _delegate.DynamicInvoke(instance.Append(arguments));
        }

        public override string ToString()
        {
            return MethodIdentifier;
        }
    }

}