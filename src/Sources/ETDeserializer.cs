using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using LiveSharp.Runtime.Virtual;
using LiveSharp.Runtime.Events;
using LiveSharp.Runtime.Expressions;
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Rewriters;
using LiveSharp.ServerClient;

namespace LiveSharp.Runtime
{
    public partial class ExpressionDeserializer
    {
        private readonly XElement _element;
        private readonly object _thisInstance;
        private readonly VirtualAssembly _virtualAssembly;
        private readonly MethodInfo[] _expressionMethods;
        private const string ReturnLabelName = "$__returnLabel";

        int _counter;
        private bool _needPatternMatchingRewrite;
        private bool _isAsync;
        private readonly MethodInfo _fieldInfoSetValueMethod = typeof(FieldInfo).GetMethod("SetValue", new[] { typeof(object), typeof(object) });
        private readonly int _debugLevel;
        private readonly string _methodIdentifier;

        public ExpressionDeserializer(XElement element, object thisInstance, VirtualAssembly virtualAssembly, int debugLevel, string methodIdentifier)
        {
            _element = element;
            
            _debugLevel = debugLevel;
            _thisInstance = thisInstance;
            _virtualAssembly = virtualAssembly;
            _expressionMethods = typeof(Expression).GetAllDeclaredMethods().ToArray();
            _methodIdentifier = methodIdentifier;
        }

        public LambdaExpression GetExpression()
        {
            var lambdaExpression = (LambdaExpression)Deserialize(_element, Scope.Init);
            return lambdaExpression;
        }

        public object Deserialize()
        {
            return Deserialize(_element, Scope.Init);
        }
        
        public LambdaExpression GetMemberInitializerExpression()
        {
            var expression = (Expression)Deserialize(_element, Scope.Init);
            return Expression.Lambda(expression);
        }

        private object Deserialize(XNode node, Scope scope)
        {
            // Declare here so it's available in the catch block
            object[] children;

            try
            {
                if (node is XText text)
                    return text.Value;

                if (node is XElement el)
                {
                    var nodeName = el.Name.LocalName;
                    var childNodes = el.Nodes().ToArray();

                    if (nodeName == "Lambda" || nodeName == "Block" || nodeName == "MakeCatchBlock")
                        scope = scope.AppendScope();

                    if (nodeName == "Lambda")
                    {
                        if (childNodes.Length != 4)
                            throw new InvalidOperationException("Lambda child nodes array length is not 3, actual length is " + childNodes.Length);

                        var parameters = (ParameterExpression[])Deserialize(childNodes[1], scope);
                        var returnType = (Type)Deserialize(childNodes[2], scope);
                        var returnLabel = Expression.Label(returnType);

                        _isAsync = (bool)Deserialize(childNodes[3], scope);

                        foreach (var parameter in parameters)
                            scope.AddElement(parameter.Name, parameter);

                        scope.AddElement(ReturnLabelName, returnLabel);

                        var body = (Expression)Deserialize(childNodes[0], scope);

                        body = AppendReturnLabel(body, returnLabel, _isAsync);
                        body = CoerceExpression(body, returnType);
                        
                        if (_debugLevel != 0) {
                            var debuggingRewriter = new DebuggingRewriter(_methodIdentifier, _thisInstance);
                            body = debuggingRewriter.RewriteLambda(body, parameters);
                        }
                        
                        //if (_needPatternMatchingRewrite)
                        body = PatternMatchingRewriter.Rewrite(body);
                        // Combine all basic expression rewriters into one? (which is only foreach atm) 
                        body = LoopRewriter.Rewrite(body);
                        
                        if (_isAsync)
                        {
                            var asyncRewriter = new AsyncRewriter();
                            body = asyncRewriter.RewriteAsyncMethod(body, returnType);
                        }

                        return Expression.Lambda(body, parameters);
                    }

                    if (nodeName == "Block")
                    {
                        var parameters = (ParameterExpression[])Deserialize(childNodes[0], scope);

                        foreach (var parameter in parameters)
                            scope.AddElement(parameter.Name, parameter);

                        InitializeLocalFunctions(el, scope);

                        var expressions = childNodes.Skip(1)
                                                    .Select(n => Deserialize(n, scope))
                                                    .Cast<Expression>()
                                                    .ToArray();

                        expressions = expressions.Length != 0 ? expressions : new Expression[] { Expression.Empty() };

                        return Expression.Block(scope.GetObjectsFromTopScope<ParameterExpression>(), expressions);
                    }

                    // Deserialize last element first for Lambda since it contains parameter definitions
                    children = el.Name != "Lambda"
                               ? childNodes.Select(n => Deserialize(n, scope)).ToArray()
                               : childNodes.Reverse().Select(n => Deserialize(n, scope)).Reverse().ToArray();

                    switch (nodeName)
                    {
                        case "This": return _thisInstance;
                        case "Base": return DeserializeBase();
                        case "Await": return new AwaitExpression((Expression)children[0], (Type)children[1]);
                        case "Null": return null;
                        case "Type": return DeserializeType(children, el.Attribute("Assembly")?.Value);
                        case "Array": return DeserializeArray(children);
                        case "FieldInfo": return DeserializeFieldInfo(children);
                        case "PropertyInfo": return DeserializePropertyInfo(children);
                        case "MethodInfo": return DeserializeMethodInfo(children);
                        case "EventInfo": return DeserializeEventInfo(children);
                        case "ConstructorInfo": return DeserializeConstructorInfo(children);
                        case "NewDelegate": return DeserializeNewDelegate(children);
                        case "Value": return DeserializeValue(children);
                        case "Params": return DeserializeParams(children);
                        case "Variable":
                        case "Parameter": return FindOrCreateParameter(scope, children);
                        case "MakeArrayType": return DeserializeMakeArrayType(children);
                        case "Assign":
                            if (RewriteAssignToNonWritableTargets(children, out var expr))
                                return expr;

                            children = CoerceAssignment(children);

                            break;
                        case "Field": return DeserializeField(children);
                        case "Property": return DeserializeProperty(children);
                        case "Bind":
                            children = HandleBind(children);
                            break;
                        case "Call":
                            if (children.Any(c => c is VirtualMethodInfo))
                                return DeserializeDynamicCall(children);
                            children = HandleCallExpression(children);

                            if (children[0] is EventInfo || children[0] is EventRefAccess)
                                return DeserializeEventInvocation(children);

                            if (children[0] is BasePlaceholder)
                                return CreateBaseCall(children);
                            break;
                        case "New":
                            if (children.Any(c => c is VirtualConstructorInfo))
                                return DeserializeDynamicCtor(children);
                            // if Length == 1 then it's a parameterless ctor call -> no need to coerce
                            if (children.Length != 1)
                                children = HandleCtor(children);
                            break;
                        case "SubscribeEvent": return SubscribeEvent(children);
                        case "UnsubscribeEvent": return UnsubscribeEvent(children);
                        case "MakeMemberAccess": return HandleMakeMemberAccess(children);
                        case "DelayedTyping": return DeserializeDelayedTyping(children);
                        case "LocalFunctionDeclaration": return DeserializeLocalFunctionDeclaration(children);
                        case "LocalFunctionCall": return DeserializeLocalFunctionCall(children, scope);
                        case "LocalFunctionRef": return DeserializeLocalFunctionRef(children, scope);
                        case "S": return DeserializeS(children);
                        case "Label": return DeserializeLabel(children, scope);
                        case "CustomLabelScope": return children.Last();
                        case "Return": return DeserializeReturn(children, scope);
                        case "While": return DeserializeWhile(children, scope);
                        case "For": return new ForExpression(
                            (Expression[])children[3], 
                            (Expression)children[4], 
                            (Expression[])children[5], 
                            (Expression[])children[6], 
                            (Expression)children[7],
                            (LabelTarget)children[0],
                            (LabelTarget)children[1],
                            (LabelTarget)children[2]);
                        case "Do": return DeserializeDo(children, scope);
                        case "ForEach": return DeserializeForeach(children, scope);
                        case "Switch": return DeserializeSwitch(children, scope);
                        case "SwitchCaseBase": return DeserializeSwitchCaseBase(children, scope);
                        case "DefaultSwitchCase": return DeserializeDefaultSwitchCase(children, scope);
                        case "IsPattern":
                            _needPatternMatchingRewrite = true;
                            return DeserializeIsPattern(children, scope);
                        case "DeclarationPattern": return DeclarationPattern(children, scope);
                        case "ConstantPattern": return ConstantPattern(children, scope);
                        case "VarPattern": return VarPattern(children, scope);
                        case "DiscardPattern": return DiscardPattern(children, scope);
                        case "RecursivePattern": return RecursivePattern(children, scope);
                        case "PositionalPatternClause": return PositionalPatternClause(children, scope);
                        case "PropertyPatternClause": return PropertyPatternClause(children, scope);
                        case "Equal":
                        case "NotEqual":
                            if (children[0] is EventInfo eventInfo)
                            {
                                var eventField = RuntimeHelpers.GetEventField(eventInfo);
                                children[0] = Expression.Field(Expression.Constant(_thisInstance), eventField);
                            }
                            else if (children[0] is EventRefAccess era)
                            {
                                var eventField = RuntimeHelpers.GetEventField(era.Event);
                                children[0] = Expression.Field(era.Target, eventField);
                            }
                            break;
                        case "TryCatch":
                            CoerceTryCatch((Expression)children[0], (CatchBlock[])children[1]);
                            break;
                        case "TryCatchFinally":
                            CoerceTryCatch((Expression)children[0], (CatchBlock[])children[2]);
                            break;
                        case "Loop":
                            children = new[] {
                                children[2],
                                children[0],
                                children[1]
                            };
                            break;
                        case "Condition":
                            {
                                var left = (Expression)children[1];
                                var right = (Expression)children[2];
                                children[2] = CoerceExpression(right, left.Type);
                                break;
                            }
                        case "Convert":
                            if (IsCustomConversion(children, out var customConversion))
                                return customConversion;
                            break;
                        case "CallBaseConstructor":
                            var arguments = (Expression[])children[0];
                            return Expression.Call(RuntimeHelpers.CallBaseConstructorMethod.Value, arguments);
                        case "PropertyChangedWrapper":
                            return CreatePropertyChangedWrapper(children, scope);
                    }

                    var compatibleMethod = GetCompatibleExpressionMethod(nodeName, children, false);
                    if (compatibleMethod != null)
                    {
                        return compatibleMethod.Invoke(null, children);
                    }

                    throw new NotImplementedException(el.Name + Environment.NewLine +
                                                      string.Join(Environment.NewLine + "  ",
                                                                  el.Nodes().Select(n => n.ToString())));
                }

                throw new NotImplementedException(node.ToString());
            }
            catch (DeserializationException) {
                throw;
            }
            catch (Exception e) {
                throw new DeserializationException("Deserialization failed for: " + Environment.NewLine + node, e, node);
            }
        }

        private object CreatePropertyChangedWrapper(object[] children, Scope scope)
        {
            var propertyName = (string)children[0];
            var originalCode = (Expression)children[1];
            var propertyNames = children.Skip(2).OfType<string>().ToArray();
            
            var valueParameter = scope.GetObject<Expression>("value");
            var thisExpr = Expression.Constant(_thisInstance);
            var property = Expression.Property(thisExpr, propertyName);
            var valueHasChanged = Expression.NotEqual(valueParameter, property);
            
            var propertyChangedEvent = thisExpr.Type.FindEvent(nameof(INotifyPropertyChanged.PropertyChanged));
            var changedVariable = Expression.Variable(propertyChangedEvent.EventHandlerType, "changed");
            var eventField = CreateEventField(thisExpr, propertyChangedEvent);
            var assignEvent = Expression.Assign(changedVariable, eventField);

            var propertyChangedArgsConstructor = typeof(PropertyChangedEventArgs).GetConstructor(new[] {typeof(string)});
            
            if (propertyChangedArgsConstructor == null)
                throw new Exception("Can't find PropertyChangedEventArgs constructor with string parameter");

            var invokeEvent = Expression.Block(propertyNames.Select(prop =>
                CreatePropertyChangedInvocation(propertyChangedArgsConstructor, prop, thisExpr, eventField)));

            var changedNotNull = Expression.NotEqual(changedVariable, Expression.Constant(null, propertyChangedEvent.EventHandlerType));
            var ifChangedNotNull = Expression.IfThen(changedNotNull, invokeEvent);
            
            var then = Expression.Block(new[] { changedVariable }, 
                originalCode, 
                assignEvent,
                ifChangedNotNull);
            
            return Expression.IfThen(valueHasChanged, then);
        }

        private static MethodCallExpression CreatePropertyChangedInvocation(ConstructorInfo propertyChangedArgsConstructor, string propertyName, ConstantExpression thisExpr, MemberExpression eventField)
        {
            var newPropertyChangedEventArgs = Expression.New(propertyChangedArgsConstructor, Expression.Constant(propertyName));
            
            return Expression.Call(RuntimeHelpers.InvokeEventFieldMethod.Value,
                new Expression[] {
                    thisExpr,
                    eventField,
                    Expression.NewArrayInit(typeof(object), thisExpr, newPropertyChangedEventArgs)
                });
        }

        private object CreateBaseCall(object[] children)
        {
            var target = Expression.Constant(_thisInstance);
            var methodToCall = (MethodInfo)children[1];
            var args = Expression.NewArrayInit(typeof(object), ((Expression[])children[2]).Select(e => Expression.Convert(e, typeof(object))));
            var methodInfo = methodToCall.ReturnType == typeof(void)
                ? RuntimeHelpers.CallBaseMethodVoidMethod.Value
                : RuntimeHelpers.CallBaseMethodReturningMethod.Value;

            return Expression.Call(methodInfo, Expression.Constant(methodToCall), target, args);
        }

        private object DeserializeBase()
        {
            return new BasePlaceholder();
        }

        private bool RewriteAssignToNonWritableTargets(object[] children, out Expression expr)
        {
            // Properties without the setter should be substituted by SetValue(backing field)
            if (children[0] is MemberExpression member && member.Member is PropertyInfo prop && prop.SetMethod == null)
            {
                Debug.Assert(children[1] is Expression, "Property assignment value should be of type Expression");

                var backingField = prop.DeclaringType.GetField(GetBackingFieldName(prop.Name), BindingFlags.Instance | BindingFlags.NonPublic);
                var value = (Expression)children[1];

                expr = CreateSetValueCall(backingField, member.Expression, value);
                return true;
            }

            // readonly fields should be substituted by SetValue(field)
            if (children[0] is MemberExpression fieldExpr && fieldExpr.Member is FieldInfo field && field.IsInitOnly)
            {
                Debug.Assert(children[1] is Expression, "Property assignment value should be of type Expression");

                var value = (Expression)children[1];

                expr = CreateSetValueCall(field, fieldExpr.Expression, value);
                return true;
            }
            
            expr = null;
            return false;
        }

        private static string GetBackingFieldName(string propertyName)
        {
            return $"<{propertyName}>k__BackingField";
        }

        private object PropertyPatternClause(object[] children, Scope scope)
        {
            var subPatterns = ((object[])children[0]).Cast<Pattern>().ToArray();

            return new PropertyPatternClause(subPatterns);
        }

        private object PositionalPatternClause(object[] children, Scope scope)
        {
            var subPatterns = ((object[])children[0]).Cast<Pattern>().ToArray();

            return new PositionalPatternClause(subPatterns);
        }

        private object RecursivePattern(object[] children, Scope scope)
        {
            var designation = children[0];
            var positionalClause = (PositionalPatternClause)children[1];
            var propertyClause = (PropertyPatternClause)children[2];
            var deconstructor = children[3];
            var inputType = (Type)children[4];

            return new RecursivePattern(designation, positionalClause, propertyClause, deconstructor, inputType);
        }

        private object DiscardPattern(object[] children, Scope scope)
        {
            var type = (Type)children[0];
            return new DiscardPattern(type);
        }

        private object VarPattern(object[] children, Scope scope)
        {
            var variableDesignation = (ParameterExpression)children[0];
            return new VarPattern(variableDesignation);
        }

        private object DeclarationPattern(object[] children, Scope scope)
        {
            var variableDesignation = (ParameterExpression)children[0];
            return new DeclarationPattern(variableDesignation);
        }

        private object ConstantPattern(object[] children, Scope scope)
        {
            var expr = (Expression)children[0];
            return new ConstantPattern(expr);
        }

        private object DeserializeSwitch(object[] children, Scope scope)
        {
            var breakLabel = (LabelTarget)children[0];
            var expression = (Expression)children[1];
            var cases = ((object[])children[2]).Cast<SwitchCaseBase>().ToArray();
            var defaultCase = children.Length >= 4 ? (DefaultSwitchCase)children[3] : null;
            
            var hasPatternCase = cases.OfType<SwitchCasePattern>().Any();

            if (hasPatternCase) {
                var expressionValue = Expression.Variable(expression.Type, GetUniqueName("__switchExpr"));

                scope.AddElement(expressionValue.Name, expressionValue);

                var assignExpressionValue = Expression.Assign(expressionValue, expression);

                return Expression.Block(assignExpressionValue, rewriteCase(0, expressionValue));

                Expression rewriteCase(int index, Expression val) {
                    var currentCase = cases[index];
                    int nextIndex = index + 1;
                    var nextCaseRewritten = nextIndex < cases.Length ? rewriteCase(nextIndex, val) : null;

                    Expression test;

                    if (currentCase is SwitchCaseExpression constant) {
                        test = Expression.Equal(val, constant.Expr);
                    } else if (currentCase is SwitchCasePattern pattern) {
                        test = new IsPatternExpression(val, pattern.Pattern);
                    } else throw new Exception("SwitchCase not supported '" + currentCase + "'");

                    var rewrittenBody = RemoveBreakRewriter.Rewrite(currentCase.Body);

                    if (nextCaseRewritten != null) {
                        return Expression.IfThenElse(test, rewrittenBody, nextCaseRewritten);
                    } else {
                        return Expression.IfThen(test, rewrittenBody);
                    }
                }
                
            } else {
                var caseExpressions = cases.Cast<SwitchCaseExpression>()
                                           .Select(c => Expression.SwitchCase(c.Body, c.Expr))
                                           .ToArray();
                
                if (defaultCase != null) {
                    var defaultBlock = Expression.Block(defaultCase.Statements);
                    var switchExpr = Expression.Switch(expression, defaultBlock, caseExpressions);

                    return Expression.Block(switchExpr, Expression.Label(breakLabel));
                } else {
                    var switchExpr = Expression.Switch(expression, caseExpressions);

                    return Expression.Block(switchExpr, Expression.Label(breakLabel));
                }
            }
        }

        private object DeserializeSwitchCaseBase(object[] children, Scope scope)
        {
            var labels = (object[])children[0];
            var body = (Expression)children[1];
            var firstLabel = labels.FirstOrDefault();

            if (firstLabel is Pattern pattern) {
                return new SwitchCasePattern(pattern, body);
            } else if (firstLabel is Expression expr) {
                return new SwitchCaseExpression(expr, body);
            }

            throw new NotImplementedException("SwitchCase label '" + firstLabel + "'");
        }

        private object DeserializeDefaultSwitchCase(object[] children, Scope scope)
        {
            var statements = (Expression[])children[0];
            return new DefaultSwitchCase(statements);

        }

        private object DeserializeIsPattern(object[] children, Scope scope)
        {
            var expression = (Expression)children[0];
            var pattern = (Pattern)children[1];

            return new IsPatternExpression(expression, pattern);
        }

        private object DeserializeMakeArrayType(object[] children)
        {
            if (children.Length != 2)
                throw new Exception("Invalid MakeArrayType definition");

            var type = (Type)children[0];
            var rank = (int)children[1];

            if (rank == 1)
                return type.MakeArrayType();
            
            return type.MakeArrayType(rank);
        }

        private bool IsCustomConversion(object[] children, out Expression customConversion)
        {
            customConversion = null;

            var convertTo = (Type)children[1];
            var isDelegate = convertTo.BaseType == typeof(MulticastDelegate) || convertTo.BaseType == typeof(Delegate);
            var isConvertableToDelegate = children[0] is MethodInfo || children[0] is LambdaExpression;

            if (isDelegate && children[0] is VirtualMethodInfo vmi) {
                throw new NotImplementedException();
                // var handler = vmi.Invoker.GetMethodLambda(_thisInstance);
                // customConversion = Expression.Call(RuntimeHelpers.CreateDelegateMethod.Value,
                //                                    Expression.Constant(convertTo),
                //                                    Expression.Constant(_thisInstance),
                //                                    Expression.Constant(handler));
                return true;
            }

            if (isDelegate && isConvertableToDelegate) {
                customConversion = Expression.Call(RuntimeHelpers.CreateDelegateMethod.Value,
                                                   Expression.Constant(convertTo),
                                                   Expression.Constant(_thisInstance),
                                                   Expression.Constant(children[0]));
                return true;
            }

            return false;
        }

        private object DeserializeEventInvocation(object[] children)
        {
            Expression eventTarget;
            EventInfo eventInfo;

            if (children[0] is EventInfo ei) {
                eventTarget = Expression.Constant(_thisInstance);
                eventInfo = ei;
            } else if (children[0] is EventRefAccess era) {
                eventTarget = era.Target;
                eventInfo = era.Event;
            } else {
                throw new Exception("Invalid DeserializeEventInvocation call: " + children[0]);
            }

            var args = (Expression[])children[2];

            return DeserializeEventInvocation(eventTarget, eventInfo, args);
        }

        private static object DeserializeEventInvocation(Expression eventTarget, EventInfo eventInfo, Expression[] args)
        {
            return Expression.Call(RuntimeHelpers.EventInvoke.Value,
                new[] {
                    eventTarget,
                    Expression.Constant(eventInfo),
                    Expression.NewArrayInit(typeof(object), args)
                });
        }

        private object DeserializeDynamicCtor(object[] children)
        {
            var virtualConstructorInfo = (VirtualConstructorInfo)children[0];
            var arguments = children[1] is object[] objs
                            ? objs.Cast<Expression>()
                                  .Select(e => e.Type != typeof(object)
                                               ? Expression.Convert(e, typeof(object))
                                               : e)
                                  .ToArray()
                            : new Expression[0];

            var createInstance = Expression.Call(Expression.Constant(_virtualAssembly),
                                                 "CreateInstance",
                                                 new [] { virtualConstructorInfo.DeclaringType },
                                                 Expression.Constant(virtualConstructorInfo),
                                                 Expression.NewArrayInit(typeof(object), arguments));
            return createInstance;
        }

        private object DeserializeDynamicCall(object[] children)
        {
            var instance = (Expression)children[0];
            var dynamicMethodInfo = (VirtualMethodInfo)children[1];
            var untypedArguments = (children[2] as object[]) ?? new object[0];
            var typedArguments = CoerceArguments(dynamicMethodInfo.GetParameterTypes(), untypedArguments);

            return GetCallMethodExpression(instance, dynamicMethodInfo, typedArguments);
        }

        private object DeserializeField(object[] children)
        {
            var instance = (Expression)children[0];
            var targetType = (Type)children[1];
            var fieldName = (string)children[2];
            var fieldType = (Type)children[3];

            var field = targetType.GetAllFields()
                                  .FirstOrDefault(f => f.Name == fieldName);

            if (field != null)
                return Expression.Field(instance, targetType, fieldName);
            
            // Check if there is an update to the field 
            // otherwise only the static version would get used
            // update:
            // This check was above static field check. I moved it below.
            // What does "update to the field" mean? It doesn't have any code like property for example
            // maybe if we add an initializer?
            var vfi = _virtualAssembly.GetVirtualFieldInfo(targetType, fieldName);
            if (vfi != null)
                return GetVirtualFieldAccess(vfi, instance);
            
            throw new Exception($"Field {fieldName} on type {targetType.Name} not found");
        }

        private object DeserializeProperty(object[] children)
        {
            var instance = (Expression)children[0];
            var targetType = (Type)children[1];
            var propertyName = (string)children[2];

            // Check if there is an update to the property
            // otherwise only the static version would get used
            var vpi = _virtualAssembly.GetVirtualPropertyInfo(targetType, propertyName);
            if (vpi != null)
                return GetVirtualPropertyAccess(vpi, instance);

            var property = targetType.FindAllProperties()
                                  .FirstOrDefault(p => p.Name == propertyName);

            if (property != null) {
                if (children.Length == 4)
                    return Expression.Property(instance, property, (Expression[]) children[3]);
                return Expression.Property(instance, targetType, propertyName);
            }
            
            throw new Exception($"Property {propertyName} on type {targetType.Name} not found");
        }

        private object DeserializeForeach(object[] children, Scope scope)
        {
            return new ForeachExpression(children);
        }

        private object DeserializeDo(object[] children, Scope scope)
        {
            var startLabel = Expression.Label(GetUniqueName("start"));
            var breakLabel = (LabelTarget)children[0];
            var continueLabel = (LabelTarget)children[1];
            var condition = (Expression)children[2];
            var statement = (Expression)children[3];

            return Expression.Block(
                typeof(void),
                new[] {
                    Expression.Label(startLabel),
                    statement,
                    Expression.Label(continueLabel),
                    Expression.IfThen(condition, Expression.Goto(startLabel)),
                    Expression.Label(breakLabel)
                }
            );
        }

        private Expression DeserializeWhile(object[] children, Scope scope)
        {
            var breakLabel = (LabelTarget)children[0];
            var continueLabel = (LabelTarget)children[1];
            var condition = (Expression)children[2];
            var statement = (Expression)children[3];
            
            return CreateWhile(condition, statement, breakLabel, continueLabel);
        }

        public static Expression CreateWhile(Expression condition, Expression statement, LabelTarget breakLabel, LabelTarget continueLabel)
        {
            return Expression.Loop(Expression.IfThenElse(condition, statement, Expression.Break(breakLabel)),
                                   breakLabel, continueLabel);
        }

        private Expression DeserializeReturn(object[] children, Scope scope)
        {
            var target = scope.GetTopmostObject<LabelTarget>(ReturnLabelName);
            if (target == null)
                throw new InvalidOperationException("Couldn't find a return label for return statement");

            var returnType = target.Type;

            if (_isAsync)
            {
                // unwrap return type
                if (returnType == typeof(Task))
                    returnType = typeof(void);
                else if (returnType.GetGenericTypeDefinition() == typeof(Task<>))
                    returnType = returnType.GenericTypeArguments[0];

                if (children.Length == 1)
                    return new AsyncReturnExpression(target, (Expression)children[0], returnType);

                return new AsyncReturnExpression(target, null, returnType);
            }

            if (children.Length == 1)
                return Expression.Return(target, (Expression)children[0], returnType);

            return Expression.Return(target);
        }

        private static void CoerceTryCatch(Expression tryBlock, CatchBlock[] catches)
        {
            var tryBlockType = tryBlock.Type;

            for (int i = 0; i < catches.Length; i++) {
                var catchBlock = catches[i];
                var catchBlockType = catchBlock.Body.Type;

                if (catchBlockType != tryBlockType) {
                    catches[i] = catchBlock.Update(catchBlock.Variable,
                                                   catchBlock.Filter,
                                                   Expression.Block(tryBlockType, catchBlock.Body, Expression.Default(tryBlockType)));
                }
            }
        }

        private object DeserializeLabel(object[] children, Scope scope)
        {
            if (children.Length == 2 && children[0] is Type labelType && children[1] is string labelId) {
                if (scope.TryGetObject<LabelTarget>(labelId, out var label))
                    return label;
                var newLabel = Expression.Label(labelType, labelId);
                scope.AddElement(labelId, newLabel);
                return newLabel;
            }

            if (children[0] is string labelId2) {
                if (scope.TryGetObject<LabelTarget>(labelId2, out var label))
                    return label;
                var newLabel = Expression.Label(labelId2);
                scope.AddElement(labelId2, newLabel);
                return newLabel;
            }

            if (children[0] is LabelTarget labelTarget) {
                return Expression.Label(labelTarget, Expression.Default(labelTarget.Type));
            }

            throw new NotImplementedException();
        }

        private object DeserializeNewDelegate(object[] children)
        {
            var delegateType = children[0];

            if (children[1] is MethodInfo methodInfo) {
                return Expression.Call(RuntimeHelpers.CreateDelegateMethod.Value,
                                        Expression.Constant(delegateType),
                                        Expression.Constant(_thisInstance),
                                        Expression.Constant(methodInfo));
            }

            if (children[1] is MethodRefAccess mra) {
                return Expression.Call(RuntimeHelpers.CreateDelegateMethod.Value, 
                                        Expression.Constant(delegateType),
                                        mra.Target,
                                        Expression.Constant(mra.Method));
            }

            if (children[1] is LambdaExpression lambda) {
                return Expression.Call(RuntimeHelpers.CreateDelegateMethod.Value, 
                    Expression.Constant(delegateType),
                    Expression.Constant(null),
                    Expression.Constant(lambda));
            }

            throw new Exception("This delegate syntax is not supported");
        }

        private object DeserializeLocalFunctionRef(object[] children, Scope scope)
        {
            var localFunctionName = (string)children[0];
            return scope.GetObject(localFunctionName);
        }

        private void InitializeLocalFunctions(XElement el, Scope scope)
        {
            var declarations = el.Nodes().OfType<XElement>().Where(n => n.Name == "LocalFunctionDeclaration");

            foreach (var declaration in declarations) {
                var declarationChildren = declaration.Nodes();
                var deserialized = declarationChildren.Select(c => Deserialize(c, scope)).ToArray();

                if (deserialized.Length != 3)
                    throw new Exception("Invalid LocalFunctionDeclaration: " + el);

                var declarationName = (string)deserialized[0];
                var localFunctionReturnType = (Type)deserialized[2];
                var lambda = CoerceLambdaReturnType((LambdaExpression)deserialized[1], localFunctionReturnType);

                scope.AddElement(declarationName, lambda);
            }
        }

        private LambdaExpression CoerceLambdaReturnType(LambdaExpression lambda, Type requiredReturnType)
        {
            if (lambda.Body.Type != requiredReturnType) {
                if (!(requiredReturnType == typeof(void)))
                    lambda = Expression.Lambda(CoerceExpression(lambda.Body, requiredReturnType), lambda.Parameters);
                else
                    lambda = Expression.Lambda(Expression.Block(new [] { lambda.Body, Expression.Empty() }), lambda.Parameters);
            }

            return lambda;
        }

        private LambdaExpression CoerceLambdaDelegateType(LambdaExpression lambda, Type delegateType)
        {
            if (lambda.Type != delegateType)
                return Expression.Lambda(delegateType, lambda.Body, lambda.Parameters);
            return lambda;
        }

        private object DeserializeLocalFunctionDeclaration(object[] children)
        {
            // This is done in InitializeLocalFunctions
            return Expression.Empty();
        }
        
        private object HandleMakeMemberAccess(object[] children)
        {
            var left = children[0] as Expression;
            var right = children[1];

            if (left != null && right is MethodInfo methodInfo)
                return new MethodRefAccess(left, methodInfo);

            if (left == null && right is MethodInfo mi) {
                if (!mi.IsStatic)
                    throw new Exception("Invalid MakeMemberAccess call with: " + mi.Name);
                return new MethodRefAccess(Expression.Constant(null), mi);
            }

            if (left != null && right is EventInfo info)
                return new EventRefAccess(left, info);

            if (right is IVirtualMemberInfo virtualMember)
                return HandleMakeMemberAccessDynamic(left, virtualMember);

            if (right is MemberInfo memberInfo)
                return Expression.MakeMemberAccess(left, memberInfo);

            throw new Exception("Invalid MakeMemberAccess parameters: " + DumpArray(children));
        }

        private object HandleMakeMemberAccessDynamic(Expression instance, IVirtualMemberInfo virtualMember)
        {
            if (virtualMember is VirtualFieldInfo vfi)
                return GetVirtualFieldAccess(vfi, instance);

            if (virtualMember is VirtualPropertyInfo vpi)
                return GetVirtualPropertyAccess(vpi, instance);
            
            if (virtualMember is VirtualMethodInfo dynamicMethodInfo)
                return GetVirtualMethod(dynamicMethodInfo.DeclaringType, dynamicMethodInfo.Name, dynamicMethodInfo.GetParameterTypes());

            throw new Exception("Unsupported IVirtualMemberInfo for MakeMemberAccess");
        }

        private static IndexExpression GetVirtualPropertyAccess(VirtualPropertyInfo vpi, Expression instance)
        {
            var accessor = Expression.New(typeof(VirtualMemberAccessor<>).MakeGenericType(vpi.PropertyType));
            return Expression.Property(accessor, "Item", Expression.Convert(instance ?? Expression.Constant(null), typeof(object)), Expression.Constant(vpi));
        }

        private static IndexExpression GetVirtualFieldAccess(VirtualFieldInfo vfi, Expression instance)
        {
            var accessor = Expression.New(typeof(VirtualMemberAccessor<>).MakeGenericType(vfi.FieldType));
            return Expression.Property(accessor, "Item", Expression.Convert(instance ?? Expression.Constant(null), typeof(object)), Expression.Constant(vfi));
        }

        private object SubscribeEvent(object[] children)
        {
            return CommonEventHandler(children, true);
        }

        private object UnsubscribeEvent(object[] children)
        {
            return CommonEventHandler(children, false);
        }

        private object CommonEventHandler(object[] children, bool isSubscribing)
        {
            Expression eventInfoTarget;
            EventInfo eventInfo;
            Expression handlerTarget;
            object handler;

            if (children[0] is EventInfo evt) {
                eventInfoTarget = Expression.Constant(_thisInstance);
                eventInfo = evt;
            } else if (children[0] is EventRefAccess era) {
                eventInfoTarget = era.Target;
                eventInfo = era.Event;
            } else {
                throw new NotImplementedException("EventHandler with `" + children[1] + "` not implemented");
            }
            
            if (children[1] is VirtualMethodInfo vmi) {
                // handlerTarget = Expression.Constant(null); // Handler target doesn't matter with Lambda 
                // handler = vmi.Invoker.GetMethodLambda(_thisInstance);
                throw new NotImplementedException();
            } else if (children[1] is MethodInfo mi) {
                handlerTarget = Expression.Constant(_thisInstance);
                handler = mi;
            } else if (children[1] is MethodRefAccess mra) {
                handlerTarget = mra.Target;
                handler = mra.Method;
            } else if (children[1] is MethodCallExpression mca && mca.Type == typeof(Delegate)) {
                var method = isSubscribing ? RuntimeHelpers.EventSubscribeWithDelegate.Value : RuntimeHelpers.EventUnsubscribeWithDelegate.Value;

                return Expression.Call(method, eventInfoTarget, Expression.Constant(eventInfo), mca);
            } else if (children[1] is LambdaExpression lambda) {
                handlerTarget = Expression.Constant(null); // Handler target doesn't matter with Lambda
                handler = lambda;
            } else {
                throw new NotImplementedException("EventHandler with `" + children[1] + "` not implemented");
            }

            return Expression.Call(isSubscribing ? RuntimeHelpers.EventSubscribe.Value : RuntimeHelpers.EventUnsubscribe.Value,
                                   eventInfoTarget,
                                   Expression.Constant(eventInfo),
                                   handlerTarget,
                                   Expression.Constant(handler));
        }

        private object[] HandleBind(object[] children)
        {
            var prop = (PropertyInfo)children[0];
            children[1] = CoerceValue(children[1], prop.PropertyType);
            return children;
        }
        
        private static object FindOrCreateParameter(Scope scope, object[] children)
        {
            var name = children.OfType<string>().FirstOrDefault();
            var type = children.OfType<Type>().FirstOrDefault();

            if (name == null || type == null)
                throw new InvalidOperationException("Invalid parameter specification");

            if (scope.TryGetObject<object>(name, out var parm))
                return parm;

            var parameter = Expression.Parameter(type, name);

            if (children.Length == 3) {
                // sometimes we don't want to put parameter in top-most scope
                // in these cases we need to go one or more scopes up
                var scopesToSkip = (int)children[2];
                scope.AddElement(name, parameter, scopesToSkip);
            } else {
                scope.AddElement(name, parameter);
            }

            return parameter;
        }
        
        private object[] HandleCallExpression(object[] children)
        {
            var methodInfo = children.OfType<MethodInfo>().FirstOrDefault();
            if (methodInfo == null)
            {
                throw new InvalidOperationException("Call expression doesn't contain MethodInfo parameter");
            }

            var isExtensionMethod = IsExtensionMethod(methodInfo);
            // do we need to skip?
            var parameters = methodInfo.GetParameters().Skip(isExtensionMethod ? 0 : 0).ToArray();
            var args = children.OfType<object[]>().FirstOrDefault();
            var hasInvocationTarget = Array.IndexOf(children, methodInfo) == 1;
            var exprArgs = CoerceArguments(parameters, args);
            
            // if (isExtensionMethod && hasInvocationTarget && false) {
            //     var invocationTarget = children[0];
            //     return new object[] {
            //         methodInfo,
            //         new [] { (Expression)invocationTarget }.Concat(exprArgs).ToArray()
            //     };
            // }

            if (hasInvocationTarget) {
                children[2] = exprArgs;
            } else {
                children[1] = exprArgs;
            }

            return children;
        }

        private object[] HandleCtor(object[] children)
        {
            var ctorInfo = children.OfType<ConstructorInfo>().FirstOrDefault();
            var parameters = ctorInfo.GetParameters().ToArray();
            var args = children.OfType<object[]>().FirstOrDefault();

            children[1] = CoerceArguments(parameters, args);

            return children;
        }

        private object[] CoerceAssignment(object[] children)
        {
            if (children[0] is EventInfo eventInfo)
                children[0] = Expression.Field(Expression.Constant(_thisInstance), RuntimeHelpers.GetEventField(eventInfo));

            if (!(children[0] is Expression left))
                throw new InvalidOperationException("Assignment target should be of type Expression");
            var leftType = GetExpressionType(left);

            children[1] = CoerceValue(children[1], leftType);

            return children;
        }

        private Expression[] CoerceArguments(ParameterInfo[] parameters, object[] args)
        {
            return CoerceArguments(parameters.Select(p => p.ParameterType).ToArray(), args);
        }

        private Expression[] CoerceArguments(Type[] parameterTypes, object[] args)
        {
            var result = new Expression[args.Length];
            for (int i = 0; i < args.Length; i++) {
                var arg = args[i];
                if (arg != null)
                    result[i] = CoerceValue(arg, parameterTypes[i]);
                else
                    result[i] = Expression.Constant(null, parameterTypes[i]);
            }
            return result;
        }

        private Expression CoerceValue(object val, Type destinationType)
        {
            if (val is Expression expr) {
                return CoerceExpression(expr, destinationType);
            } else if (val is VirtualMethodInfo vmi) {
                throw new NotImplementedException();
                // var handler = vmi.Invoker.GetMethodLambda(_thisInstance);
                // var createDel = Expression.Call(RuntimeHelpers.CreateDelegateMethod.Value,
                //     Expression.Constant(destinationType),
                //     Expression.Constant(_thisInstance),
                //     Expression.Constant(handler));
                //
                // return Expression.Convert(createDel, destinationType);
            }  else if (val is MethodInfo mi) {
                return CreateDelegateExpression(null, mi, destinationType);
            } else if (val is MethodRefAccess mra) {
                return CreateDelegateExpression(mra.Target, mra.Method, destinationType);
            } else if (val is EventInfo eventInfo) {
                return CreateEventField(Expression.Constant(_thisInstance), eventInfo);
            }else {
                throw new NotImplementedException("CoerceValue for " + val.GetType().FullName + " to " + destinationType.FullName + " not implemented");
            }
        }

        private MemberExpression CreateEventField(Expression eventTarget, EventInfo eventInfo)
        {
            return Expression.Field(eventTarget, RuntimeHelpers.GetEventField(eventInfo));
        }

        private Expression CoerceExpression(Expression expr, Type destinationType)
        {
            if (destinationType == null)
                throw new ArgumentNullException(nameof(destinationType));

            if (destinationType == typeof(void))
                return expr;

            if (destinationType.IsByRef)
                destinationType = destinationType.GetElementType();
            
            if (expr is LambdaExpression lambda && destinationType.IsDelegate()) {
                var returnType = destinationType.GetMethod("Invoke").ReturnType;
                expr = CoerceLambdaReturnType(lambda, returnType);

                if (expr.Type == destinationType)
                    return expr;

                return CoerceLambdaDelegateType(lambda, destinationType);
            }

            var exprType = expr.Type;
            if (exprType == destinationType)
                return expr;

            if (expr.NodeType == ExpressionType.Throw && expr is UnaryExpression unary) {
                return Expression.Throw(unary.Operand, destinationType);
            }

            if (expr is ConstantExpression constantExpression && exprType == typeof(object) && constantExpression.Value == null)
                return Expression.Constant(null, destinationType);

            if (CanConvert(exprType, destinationType))
                return Expression.Convert(expr, destinationType);

            foreach (var method in destinationType.GetAllDeclaredMethods()) {
                if (method.Name == "op_Implicit") {
                    var parameters = method.GetParameters();
                    if (parameters.Length != 1)
                        continue;

                    var parmType = parameters[0].ParameterType;
                    if (parmType == destinationType) {
                        return Expression.Convert(expr, destinationType, method);
                    } else if (CanConvert(exprType, parmType)) {
                        var firstConvert = Expression.Convert(expr, parmType);
                        return Expression.Convert(firstConvert, destinationType);
                    }
                }
            }

            if(destinationType.IsConstructedGenericType &&
               destinationType.GetGenericTypeDefinition() == typeof(Expression<>) &&
               destinationType.GetGenericArguments()[0] == exprType) {
                return Expression.Constant(expr);
            }

            try {
                return Expression.Convert(expr, destinationType);
            } catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }

        private bool CanConvert(Type from, Type to)
        {
            return to.IsAssignableFrom(from) ||
                   NumericTypes.TypeIsNumeric(from) && NumericTypes.TypeIsNumeric(to);
        }

        private Expression CreateDelegateExpression(Expression instance, MethodInfo mi, Type delegateType)
        {
            var methodInfoType = typeof(MethodInfo);

            if (mi != null) {
                var delegateTypeExpr = Expression.Constant(delegateType);
                var createDelegateMethod = methodInfoType.GetMethod("CreateDelegate", true, new[] { typeof(Type), typeof(object) }); ;
                var miConstant = Expression.Constant(mi);
            
                Expression callExpression;
                if (mi.IsStatic) {
                    callExpression = Expression.Call(miConstant, createDelegateMethod, delegateTypeExpr, Expression.Constant(null, typeof(object)));
                } else {
                    instance = instance ?? Expression.Constant(_thisInstance);
                    callExpression = Expression.Call(miConstant, createDelegateMethod, delegateTypeExpr, instance);
                }
            
                return Expression.Convert(callExpression, delegateType);
            } else {
                return null;
            }

        }

        
        private Type GetExpressionType(Expression expression)
        {
            return expression.Type;
        }

        private object DeserializeMethodInfo(object[] children)
        {
            var containingType = (Type)children[0];

            // Is generic method?
            if (!(children[2] is string)) {
                var typeArguments = (Type[])children[2];
                var methodName = (string)children[3];
                var argumentTypes = children.Skip(4).OfType<Type>().ToArray();
                var method = GetCompatibleMethod(containingType, methodName, argumentTypes, typeArguments);

                return method ?? throw new Exception($"MethodInfo {methodName} not found");
            } else {
                var methodName = (string)children[2];
                var argumentTypes = children.Skip(3).OfType<Type>().ToArray();
                var returnType = (Type)children[1];

                return GetCompatibleMethod(containingType, methodName, argumentTypes, throwIfNotFound: false) ??
                       GetVirtualMethod(containingType, methodName, argumentTypes)
                       ?? throw new Exception($"MethodInfo {methodName} not found");
            }
        }

        private object GetVirtualMethod(Type containingType, string methodName, Type[] parameterTypes)
        {
            return _virtualAssembly.GetVirtualMethodInfo(containingType, methodName, parameterTypes);
        }

        private object DeserializeArray(IReadOnlyList<object> children)
        {
            var arrayType = (Type)children[0];
            var array = Array.CreateInstance(arrayType, children.Count - 1);

            for (int i = 1; i < children.Count; i++)
                array.SetValue(children[i], i - 1);

            return array;
        }
        
        private Expression[] DeserializeParams(object[] children)
        {
            return children.OfType<Expression>().ToArray();
        }

        private string DeserializeS(object[] children)
        {
            if (children.Length == 0) return "";
            return (string)children[0];
        }

        private object DeserializeValue(object[] children)
        {
            // Only type is provided, meaning it's null
            if (children.Length == 1)
                return null;

            if (children.Length != 2)
                throw new ArgumentException(nameof(children));

            var val = children[1];
            if (!(val is string))
                return val;

            var valString = (string)val;
            var type = (Type)children[0];

            if (NumericTypes.TypeIsNumeric(type)) {
                decimal res;
                if (decimal.TryParse(valString, NumberStyles.Any, CultureInfo.InvariantCulture, out res))
                    return Convert.ChangeType(res, type, null);
                throw new FormatException("Couldn't parse number '" + valString + "' typed as " + type.Name);
            }
            if (type.GetTypeInfo().IsEnum)
                return Enum.Parse(type, valString.Substring(valString.LastIndexOf(".") + 1), true);

            if (type == typeof(bool))
                return bool.Parse(valString);

            if (type == typeof(char) && valString.Length > 0)
                return valString[0];

            return valString;
        }

        private object DeserializeConstructorInfo(object[] children)
        {
            if (children.Length == 0)
                throw new ArgumentException(nameof(children));

            var parentType = (Type)children[0];
            var parameterTypes = children.Skip(1).Cast<Type>().ToArray();

            var compiledConstructorInfo = KnownTypes.GetConstructor(parentType, parameterTypes, throwIfNotFound: false);
            if (compiledConstructorInfo != null && parentType.BaseType != typeof(VirtualTypeBase))
                return compiledConstructorInfo;

            var virtualConstructorInfo = _virtualAssembly.GetVirtualConstructorInfo(parentType, parameterTypes);
            if (virtualConstructorInfo != null)
                return virtualConstructorInfo;
            else if (compiledConstructorInfo != null)
                return compiledConstructorInfo;
            
            throw new Exception($"Constructor for type {parentType.FullName} with parameters '{string.Join("," ,parameterTypes.Select(p => p.Name))}' not found");
        }

        private object DeserializeFieldInfo(object[] children)
        {
            var type = (Type)children[0];
            var fieldName = (string)children[1];
            var fieldType = (Type)children[2];
            var field = type.FindField(fieldName);

            if (field != null)
                return field;

            var vfi = _virtualAssembly.GetVirtualFieldInfo(type, fieldName);
            if (vfi != null)
                return vfi;
            
            throw new Exception($"Field {fieldName} not found on type {type.Name}");
        }

        private object DeserializePropertyInfo(object[] children)
        {
            var type = (Type)children[0];
            var propertyName = (string)children[1];
            var propertyType = (Type)children[2];
            var property = type.FindProperty(propertyName);

            if (property != null)
                return property;
            
            var virtualPropertyInfo = _virtualAssembly.GetVirtualPropertyInfo(type, propertyName);
            if (virtualPropertyInfo != null)
                return virtualPropertyInfo;
            
            throw new Exception($"Property {propertyName} not found on type {type?.FullName}");
        }

        private object DeserializeEventInfo(object[] children)
        {
            var type = (Type)children[0];
            var eventName = (string)children[1];
            return type.FindEvent(eventName);
        }

        private object DeserializeType(object[] children, string assembly)
        {
            var parent = children[0];
            var typeName = (string)children[1];
            var typeArguments = new List<Type>();

            Type type;

            if (parent is string namespaceName) {
                var fullTypeName = namespaceName + "." + typeName;
                type = KnownTypes.FindType(fullTypeName, assembly);
                if (type == null)
                    return GetDynamicType(fullTypeName);
            } else if (parent is Type parentType) {
                type = parentType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                                 .FirstOrDefault(t => t.Name == typeName);
                
                if (type == null)
                    return GetDynamicType(parentType.FullName + "+" + typeName);

                if (type.IsGenericTypeDefinition)
                    typeArguments.AddRange(parentType.GenericTypeArguments);
            } else {
                throw new InvalidOperationException("Invalid parent for type definition: " + parent);
            }

            if (type.IsGenericTypeDefinition) {
                typeArguments.AddRange(children.Skip(2).OfType<Type>());
                if (typeArguments.Count > 0)
                    return type.MakeGenericType(typeArguments.ToArray());
                
                // Maybe it's type definition?
                return type;
            }

            return type;
        }

        private object GetDynamicType(string fullTypeName)
        {
            return _virtualAssembly.GetDynamicType(fullTypeName);
        }

        private object DeserializeLocalFunctionCall(object[] children, Scope scope)
        {
            var localFunctionName = (string)children[0];
            
            var lambdaInstance = scope.GetObject<LambdaExpression>(localFunctionName);
            var parameterTypes = lambdaInstance.Parameters.Select(p => typeof(object)).ToArray();
            var coercedArgs = CoerceArguments(parameterTypes, (object[])children[1]);
            //var coercedArgsArray = Expression.Constant((object[])children[1]);
            var args = (object[])children[1];
            var argArray = Expression.NewArrayInit(typeof(object), coercedArgs);
            var dynamicInvokeCall = Expression.Call(lambdaInstance, "DynamicInvoke", null, argArray);

            if (lambdaInstance.ReturnType != typeof(void)) {
                return Expression.Convert(dynamicInvokeCall, lambdaInstance.ReturnType);
            } else {
                return dynamicInvokeCall;
            }
        }

        private MethodInfo GetCompatibleExpressionMethod(string elementName, object[] arguments, bool exactArgumentTypes = true, bool throwIfNotFound = true)
        {
            var argumentTypes = arguments.Select(a => a != null ? a.GetType() : null).ToArray();
            return GetCompatibleMethod(_expressionMethods, elementName, argumentTypes, null, exactArgumentTypes, throwIfNotFound);
        }

        private MethodInfo GetCompatibleMethod(Type containingType, string methodName, Type[] argumentTypes, Type[] genericTypeArguments = null, bool exactArgumentTypes = true, bool throwIfNotFound = true)
        {
            return GetCompatibleMethod(containingType.GetAllMethods(), methodName, argumentTypes, genericTypeArguments, exactArgumentTypes, throwIfNotFound);
        }

        private MethodInfo GetCompatibleMethod(IEnumerable<MethodInfo> methodList, string methodName, Type[] argumentTypes, Type[] genericTypeArguments = null, bool exactArgumentTypes = true, bool throwIfNotFound = true)
        {
            genericTypeArguments ??= new Type[0];

            foreach (var methodInfo in methodList) {
                var mi = methodInfo;
                if (mi.Name != methodName)
                    continue;
                
                if (mi.IsGenericMethod)
                    if (genericTypeArguments.Length == mi.GetGenericArguments().Length)
                        mi = mi.MakeGenericMethod(genericTypeArguments);
                    else continue;
                else if (genericTypeArguments.Length > 0) // caller expects generic method and it's not
                    continue;
                
                var parms = mi.GetParameters().ToArray();
                var parmCount = parms.Length;

                if (parmCount != argumentTypes.Length)
                    continue;

                var argumentTypesMatch = true;

                for (int i = 0; i < parmCount && argumentTypesMatch; i++) {
                    var parameterType = parms[i].ParameterType;
                    var argumentType = argumentTypes[i];

                    if (argumentType == null)
                        continue;

                    // Fixes RuntimeType vs Type inconsistency
                    if (parameterType == typeof(Type) && (argumentType == typeof(Type) || argumentType.Name == "RuntimeType"))
                        continue;

                    if (parameterType.IsByRef && !argumentType.IsByRef)
                        argumentType = argumentType.MakeByRefType();

                    if (exactArgumentTypes) {
                        if (parameterType != argumentType)
                            argumentTypesMatch = false;
                    } else {
                        if (!parameterType.IsAssignableFrom(argumentType))
                            argumentTypesMatch = false;
                    }
                }

                if (argumentTypesMatch)
                    return mi;
            }

            if (throwIfNotFound)
                throw new Exception("MethodInfo not found " + methodName + " (" + string.Join(", ", argumentTypes.Select(t => t.Name)) + ")");

            return null;
        }

        private bool IsExtensionMethod(MethodInfo method)
        {
            return method.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");
        }

        private string DumpArray(object[] array)
        {
            return string.Join(", ", array.Select(c => c?.ToString() ?? "null"));
        }

        private object DeserializeDelayedTyping(object[] children)
        {
            if (children.Length != 1)
                throw new InvalidOperationException("Invalid children count for DelayedTyping node: " + children.Length);

            return new DelayedTyping(children[0]);
        }

        private Expression AppendReturnLabel(Expression lambdaBody, LabelTarget returnLabelTarget, bool isAsync)
        {
            if (lambdaBody is BlockExpression block) {
                var returnLabel = Expression.Label(returnLabelTarget, Expression.Default(returnLabelTarget.Type));
                var updatedExpressions = block.Expressions
                                              .Concat(new Expression[] { returnLabel });

                return Expression.Block(block.Variables, updatedExpressions);
            }
            
            if (isAsync && lambdaBody.Type != returnLabelTarget.Type) {
                var returnLabel = Expression.Label(returnLabelTarget, Expression.Default(returnLabelTarget.Type));
                return Expression.Block(lambdaBody, returnLabel);
            }
            
            return lambdaBody;
        }

        private string GetUniqueName(string nameBase)
        {
            return $"__{nameBase}${_counter++}";
        }

        private Expression GetCallMethodExpression(Expression instance, VirtualMethodInfo virtualMethodInfo, Expression[] arguments)
        {
            arguments = arguments.Select(e => e.Type != typeof(object)
                                              ? Expression.Convert(e, typeof(object))
                                              : e)
                                 .ToArray();

            var invoker = virtualMethodInfo.Invoker;
            
            if (virtualMethodInfo.ReturnType != typeof(void)) {
                return Expression.Call(Expression.Constant(invoker),
                                       "InvokeMethod",
                                       new [] { virtualMethodInfo.ReturnType }, 
                                       new Expression[] {
                                           Expression.Convert(instance ?? Expression.Constant(null), typeof(object)), 
                                           Expression.NewArrayInit(typeof(object), arguments)
                                       });
            }

            return Expression.Call(Expression.Constant(invoker),
                "InvokeMethodVoid",
                null,
                new [] {
                    instance ?? Expression.Constant(null),
                    Expression.NewArrayInit(typeof(object), arguments)
                });
        }
            
        Expression CreateSetValueCall(FieldInfo backingField, Expression expression, Expression value)
        {
            return Expression.Call(Expression.Constant(backingField),
                                   _fieldInfoSetValueMethod,
                                   Expression.Convert(expression, typeof(object)),
                                   Expression.Convert(value, typeof(object)));
        }
        
        class BasePlaceholder
        {}

        class DeserializationException : Exception
        {
            public XNode Node { get; }
            public string NodeString { get; }
            public DeserializationException(string message, Exception inner, XNode node) : base(message, inner)
            {
                Node = node;
                NodeString = node?.ToString();
            }
        }
    }
}
