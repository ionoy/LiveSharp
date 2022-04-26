using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using LiveSharp.Runtime.Virtual;
using LiveSharp.Shared;

// ReSharper disable IdentifierTypo

namespace LiveSharp.Runtime.IL
{
    public class IlExpressionCompiler
    {
        private readonly VirtualMethodInfo _methodInfo;
        private readonly ILogger _logger;

        private static readonly Dictionary<short, Func<InstructionContext, Expression>> Handlers = CreateHandlers();
        private LambdaExpression _lambdaExpression;

        public IlExpressionCompiler(VirtualMethodInfo methodInfo, ILogger logger)
        {
            _methodInfo = methodInfo;
            _logger = logger;
        }

        public Delegate GetDelegate(VirtualMethodBody methodBody, bool enableDebugging = true, Type delegateType = null)
        {
            var parameters = _methodInfo
                .Parameters
                .Select(p => Expression.Parameter(CompilerHelpers.ResolveVirtualType(p.ParameterType), p.ParameterName))
                .ToList();

            var locals = methodBody.Locals
                .Select(p => Expression.Parameter(CompilerHelpers.ResolveVirtualType(p.LocalType), p.LocalName))
                .ToList();

            var localMap = locals.Select((expr, i) => (local: methodBody.Locals[i], expr))
                .ToDictionary(t => t.local, t => t.expr);

            if (!_methodInfo.IsStatic) {
                // prepend 'this'
                parameters.Insert(0, Expression.Parameter(typeof(object), "this"));
                locals.Add(Expression.Parameter(CompilerHelpers.ResolveVirtualType(_methodInfo.DeclaringType), "castedThis"));
            }
            
            var context = new CompilerContext(_methodInfo, methodBody, parameters, locals, localMap, CompileInstruction, _logger);
            var expressions = context.CompileInstructions().ToList();
            
            InsertLabelTargets(context, expressions);

            if (methodBody.TryBlocks.Length > 0) 
                expressions = InsertTryBlocks(expressions, methodBody.TryBlocks.OrderBy(t => t.Start.Index).ToArray());

            if (!_methodInfo.IsStatic)
                expressions.Insert(0, Expression.Assign(context.CastedThis, Expression.Convert(parameters[0], CompilerHelpers.ResolveVirtualType(_methodInfo.DeclaringType))));
            
            var blockParameters = locals.Concat(context.Registers);
            var block = (Expression)Expression.Block(blockParameters, expressions);
            
            block = CompilerHelpers.Coerce(block, _methodInfo.ReturnType);

            //try {
                _lambdaExpression = delegateType == null 
                    ? Expression.Lambda(block, parameters) 
                    : Expression.Lambda(delegateType, block, parameters);
            //}
            // catch (TypeLoadException) {
            //     // TypeLoadException usually happens if delegateType contains a virtual or missing type
            //     // this should be fixed in .netstandard 2.1 because we can build it  
            //     _lambdaExpression = Expression.Lambda(block, parameters);
            // }
            
            return _lambdaExpression.Compile();
        }

        private List<Expression> InsertTryBlocks(IReadOnlyList<Expression> expressions,
            IReadOnlyList<TryBlockInfo> tryBlocks)
        {
            var result = new List<Expression>();
            var tryBlockStack = new Stack<TryBlockInfo>();
            var tryBlocksAccus = new Stack<(List<Expression> tryList, List<Expression> handlerList, List<Expression> filterList)>();
            var mergedTryStack = new Stack<TryExpression>();
            
            for (int i = 0; i < expressions.Count; i++) {
                var index = i;
                var tryBlocksAtIndex = tryBlocks
                    .Where(t => t.Start.Index == index)
                    .OrderByDescending(tryInfo => tryInfo.HandlerEnd.Index);

                foreach (var tryBlockInfo in tryBlocksAtIndex) {
                    tryBlockStack.Push(tryBlockInfo);
                    tryBlocksAccus.Push((tryList: new List<Expression>(), handlerList: new List<Expression>(), filterList: new List<Expression>()));
                }

                if (tryBlockStack.Count == 0) {
                    result.Add(expressions[i]);
                } else if (tryBlockStack.Count > 0) {
                    var currentTryBlock = tryBlockStack.Peek();
                    var currentTryBlockAccu = tryBlocksAccus.Peek();
                    
                    // Unless we reached the Handler end, we should always have some try-accu to add expression to
                    var accuList = getTryBlockAccumulatorList(currentTryBlockAccu, currentTryBlock, index);
                    accuList.Add(expressions[i]);
                    
                    if (index == currentTryBlock.HandlerEnd.Index - 1) {
                        // Current Try block is completed
                        // 1) instantiate it
                        // 2) pass it to the outer scope
                        tryBlockStack.Pop();
                        tryBlocksAccus.Pop();
                        
                        var tryBlock = Expression.Block(currentTryBlockAccu.tryList);
                        
                        if (currentTryBlock.HandlerType == "Catch" || currentTryBlock.HandlerType == "Filter") {
                            var catchType = currentTryBlock.CatchType ?? typeof(object);
                            var catchBody = Expression.Block(typeof(void), currentTryBlockAccu.handlerList);
                            var filterBody = currentTryBlock.FilterStart != null
                                ? CompilerHelpers.Coerce(Expression.Block(currentTryBlockAccu.filterList), typeof(bool))
                                : null;
                            
                            var catchBlock = Expression.MakeCatchBlock(catchType, currentTryBlock.ExceptionParameter, catchBody, filterBody);
                            var tryExpr = prependMergedHandlers(Expression.TryCatch(tryBlock, catchBlock));
                            
                            pushToOuterScope(tryExpr);
                            
                            
                        } else if (currentTryBlock.HandlerType == "Finally") {
                            var finallyBody = Expression.Block(currentTryBlockAccu.handlerList);
                            var tryExpr = prependMergedHandlers(Expression.TryFinally(tryBlock, finallyBody));
                            
                            pushToOuterScope(tryExpr);
                        }

                        TryExpression prependMergedHandlers(TryExpression tryExpr)
                        {
                            foreach (var mergedTry in mergedTryStack) {
                                var newHandlers = mergedTry.Handlers.Concat(tryExpr.Handlers);
                                var body = tryExpr.Body;

                                if (body is BlockExpression block && block.Expressions.Count == 0)
                                    body = mergedTry.Body;
                                
                                tryExpr = tryExpr.Update(body, newHandlers, tryExpr.Finally, tryExpr.Fault);
                            }

                            return tryExpr;
                        }
                        void pushToOuterScope(TryExpression tryExpr)
                        {
                            if (tryBlockStack.Count > 0) {
                                var outerTryBlock = tryBlockStack.Peek();
                                var outerTryBlockAccu = tryBlocksAccus.Peek();

                                // Merged try's
                                if (currentTryBlock.Start == outerTryBlock.Start &&
                                    currentTryBlock.End == outerTryBlock.End) {
                                    
                                    if (tryExpr.Finally != null)
                                        throw new NotImplementedException("Finally to merge");
                                    
                                    if (tryExpr.Handlers.Count == 0)
                                        throw new NotImplementedException("Handlers is empty, cannot merge");
 
                                    mergedTryStack.Push(tryExpr);
                                } else {
                                    var list = getTryBlockAccumulatorList(outerTryBlockAccu, outerTryBlock, index);
                                    list.Add(tryExpr);
                                }
                            } else {
                                result.Add(tryExpr);
                            }
                        }
                    }
                }
            }
            
            return result;

            List<Expression> getTryBlockAccumulatorList((List<Expression> tryList, List<Expression> handlerList, List<Expression> filterList) tryBlockAccu, TryBlockInfo tryBlock, in int index)
            {
                if (index >= tryBlock.Start?.Index && index < tryBlock.End?.Index)
                    return tryBlockAccu.tryList;
                if (tryBlock.FilterStart?.Index != -1 && index >= tryBlock.FilterStart?.Index && index < tryBlock.HandlerStart?.Index)
                    return tryBlockAccu.filterList;
                if (index >= tryBlock.HandlerStart?.Index && index < tryBlock.HandlerEnd?.Index)
                    return tryBlockAccu.handlerList;
                
                throw new InvalidOperationException($"Index {index} is not inside the try block {tryBlock}");
            }
        }


        private static void InsertLabelTargets(CompilerContext context, List<Expression> expressions)
        {
            // Start from latest label and move down, so further indices stay the same
            foreach (var valuePair in context.LabelTargets.OrderByDescending(kvp => kvp.Key)) {
                var index = valuePair.Key;
                var labelTarget = valuePair.Value;

                expressions[index] = Expression.Block(Expression.Label(labelTarget), expressions[index]);
                //expressions.Insert(index, Expression.Label(labelTarget));
            }
        }

        private static Expression CompileInstruction(InstructionContext instructionContext)
        {
            try {
                if (Handlers.TryGetValue(instructionContext.Instruction.OpCode.Value, out var handler))
                    return handler(instructionContext);
            }
            catch (Exception e) {
                throw new AggregateException($"Instruction compilation failed: {instructionContext.Instruction}", e);
            }

            throw new NotImplementedException("Instruction " + instructionContext.Instruction.OpCode);
        }

        private static Dictionary<short, Func<InstructionContext, Expression>> CreateHandlers()
        {
            var handlers = new Dictionary<short, Func<InstructionContext, Expression>> {
                {OpCodes.Ret.Value, IlExpressionCompilerHandlers.Ret},
                {OpCodes.Nop.Value, IlExpressionCompilerHandlers.Nop},
                {OpCodes.Call.Value, IlExpressionCompilerHandlers.Call},
                {OpCodes.Ldarg_0.Value, IlExpressionCompilerHandlers.Ldarg_0},
                {OpCodes.Ldarg_1.Value, IlExpressionCompilerHandlers.Ldarg_1},
                {OpCodes.Ldarg_2.Value, IlExpressionCompilerHandlers.Ldarg_2},
                {OpCodes.Ldarg_3.Value, IlExpressionCompilerHandlers.Ldarg_3},
                {OpCodes.Ldarg.Value, IlExpressionCompilerHandlers.Ldarg},
                {OpCodes.Ldarga.Value, IlExpressionCompilerHandlers.Ldarga},
                {OpCodes.Ldarg_S.Value, IlExpressionCompilerHandlers.Ldarg_S},
                {OpCodes.Ldarga_S.Value, IlExpressionCompilerHandlers.Ldarga_S},
                {OpCodes.Callvirt.Value, IlExpressionCompilerHandlers.Callvirt},
                {OpCodes.Ldloc_0.Value, IlExpressionCompilerHandlers.Ldloc_0},
                {OpCodes.Ldloc_1.Value, IlExpressionCompilerHandlers.Ldloc_1},
                {OpCodes.Ldloc_2.Value, IlExpressionCompilerHandlers.Ldloc_2},
                {OpCodes.Ldloc_3.Value, IlExpressionCompilerHandlers.Ldloc_3},
                {OpCodes.Ldloc.Value, IlExpressionCompilerHandlers.Ldloc},
                {OpCodes.Ldloc_S.Value, IlExpressionCompilerHandlers.Ldloc_S},
                {OpCodes.Stloc_0.Value, IlExpressionCompilerHandlers.Stloc_0},
                {OpCodes.Stloc_1.Value, IlExpressionCompilerHandlers.Stloc_1},
                {OpCodes.Stloc_2.Value, IlExpressionCompilerHandlers.Stloc_2},
                {OpCodes.Stloc_3.Value, IlExpressionCompilerHandlers.Stloc_3},
                {OpCodes.Stloc.Value, IlExpressionCompilerHandlers.Stloc},
                {OpCodes.Stloc_S.Value, IlExpressionCompilerHandlers.Stloc_S},
                {OpCodes.Ldc_I4.Value, IlExpressionCompilerHandlers.Ldc_I4},
                {OpCodes.Ldc_I4_S.Value, IlExpressionCompilerHandlers.Ldc_I4_S},
                {OpCodes.Ldc_I4_0.Value, IlExpressionCompilerHandlers.Ldc_I4_0},
                {OpCodes.Ldc_I4_1.Value, IlExpressionCompilerHandlers.Ldc_I4_1},
                {OpCodes.Ldc_I4_2.Value, IlExpressionCompilerHandlers.Ldc_I4_2},
                {OpCodes.Ldc_I4_3.Value, IlExpressionCompilerHandlers.Ldc_I4_3},
                {OpCodes.Ldc_I4_4.Value, IlExpressionCompilerHandlers.Ldc_I4_4},
                {OpCodes.Ldc_I4_5.Value, IlExpressionCompilerHandlers.Ldc_I4_5},
                {OpCodes.Ldc_I4_6.Value, IlExpressionCompilerHandlers.Ldc_I4_6},
                {OpCodes.Ldc_I4_7.Value, IlExpressionCompilerHandlers.Ldc_I4_7},
                {OpCodes.Ldc_I4_8.Value, IlExpressionCompilerHandlers.Ldc_I4_8},
                {OpCodes.Ldc_R4.Value, IlExpressionCompilerHandlers.Ldc_R4},
                {OpCodes.Ldc_R8.Value, IlExpressionCompilerHandlers.Ldc_R8},
                {OpCodes.Ldstr.Value, IlExpressionCompilerHandlers.Ldstr},
                {OpCodes.Ldtoken.Value, IlExpressionCompilerHandlers.Ldtoken},
                {OpCodes.Ldnull.Value, IlExpressionCompilerHandlers.Ldnull},
                {OpCodes.Dup.Value, IlExpressionCompilerHandlers.Dup},
                {OpCodes.Newarr.Value, IlExpressionCompilerHandlers.Newarr},
                {OpCodes.Brtrue_S.Value, IlExpressionCompilerHandlers.Brtrue_S},
                {OpCodes.Brfalse_S.Value, IlExpressionCompilerHandlers.Brfalse_S},
                {OpCodes.Brtrue.Value, IlExpressionCompilerHandlers.Brtrue},
                {OpCodes.Brfalse.Value, IlExpressionCompilerHandlers.Brfalse},
                {OpCodes.Br_S.Value, IlExpressionCompilerHandlers.Br_S},
                {OpCodes.Br.Value, IlExpressionCompilerHandlers.Br},
                {OpCodes.Switch.Value, IlExpressionCompilerHandlers.Switch},
                {OpCodes.Clt.Value, IlExpressionCompilerHandlers.Clt},
                {OpCodes.Clt_Un.Value, IlExpressionCompilerHandlers.Clt_Un},
                {OpCodes.Cgt.Value, IlExpressionCompilerHandlers.Cgt},
                {OpCodes.Cgt_Un.Value, IlExpressionCompilerHandlers.Cgt_Un},
                {OpCodes.Ceq.Value, IlExpressionCompilerHandlers.Ceq},
                {OpCodes.Add.Value, IlExpressionCompilerHandlers.Add},
                {OpCodes.Add_Ovf.Value, IlExpressionCompilerHandlers.Add_Ovf},
                {OpCodes.Add_Ovf_Un.Value, IlExpressionCompilerHandlers.Add_Ovf_Un},
                {OpCodes.Sub.Value, IlExpressionCompilerHandlers.Sub},
                {OpCodes.Sub_Ovf.Value, IlExpressionCompilerHandlers.Sub_Ovf},
                {OpCodes.Sub_Ovf_Un.Value, IlExpressionCompilerHandlers.Sub_Ovf_Un},
                {OpCodes.Ldloca.Value, IlExpressionCompilerHandlers.Ldloca},
                {OpCodes.Ldloca_S.Value, IlExpressionCompilerHandlers.Ldloca_S},
                {OpCodes.Initobj.Value, IlExpressionCompilerHandlers.Initobj},
                {OpCodes.Newobj.Value, IlExpressionCompilerHandlers.Newobj},
                {OpCodes.Ldelem.Value, IlExpressionCompilerHandlers.Ldelem},
                {OpCodes.Ldelema.Value, IlExpressionCompilerHandlers.Ldelema},
                {OpCodes.Ldelem_I.Value, IlExpressionCompilerHandlers.Ldelem_I},
                {OpCodes.Ldelem_I1.Value, IlExpressionCompilerHandlers.Ldelem_I1},
                {OpCodes.Ldelem_I2.Value, IlExpressionCompilerHandlers.Ldelem_I2},
                {OpCodes.Ldelem_I4.Value, IlExpressionCompilerHandlers.Ldelem_I4},
                {OpCodes.Ldelem_I8.Value, IlExpressionCompilerHandlers.Ldelem_I8},
                {OpCodes.Ldelem_R4.Value, IlExpressionCompilerHandlers.Ldelem_R4},
                {OpCodes.Ldelem_R8.Value, IlExpressionCompilerHandlers.Ldelem_R8},
                {OpCodes.Ldelem_Ref.Value, IlExpressionCompilerHandlers.Ldelem_Ref},
                {OpCodes.Ldelem_U1.Value, IlExpressionCompilerHandlers.Ldelem_U1},
                {OpCodes.Ldelem_U2.Value, IlExpressionCompilerHandlers.Ldelem_U2},
                {OpCodes.Ldelem_U4.Value, IlExpressionCompilerHandlers.Ldelem_U4},
                {OpCodes.Stelem.Value, IlExpressionCompilerHandlers.Stelem},
                {OpCodes.Stelem_I.Value, IlExpressionCompilerHandlers.Stelem_I},
                {OpCodes.Stelem_I1.Value, IlExpressionCompilerHandlers.Stelem_I1},
                {OpCodes.Stelem_I2.Value, IlExpressionCompilerHandlers.Stelem_I2},
                {OpCodes.Stelem_I4.Value, IlExpressionCompilerHandlers.Stelem_I4},
                {OpCodes.Stelem_I8.Value, IlExpressionCompilerHandlers.Stelem_I8},
                {OpCodes.Stelem_R4.Value, IlExpressionCompilerHandlers.Stelem_R4},
                {OpCodes.Stelem_R8.Value, IlExpressionCompilerHandlers.Stelem_R8},
                {OpCodes.Stelem_Ref.Value, IlExpressionCompilerHandlers.Stelem_Ref},
                {OpCodes.Conv_I.Value, IlExpressionCompilerHandlers.Conv_I},
                {OpCodes.Conv_I1.Value, IlExpressionCompilerHandlers.Conv_I1},
                {OpCodes.Conv_I2.Value, IlExpressionCompilerHandlers.Conv_I2},
                {OpCodes.Conv_I4.Value, IlExpressionCompilerHandlers.Conv_I4},
                {OpCodes.Conv_I8.Value, IlExpressionCompilerHandlers.Conv_I8},
                {OpCodes.Conv_R4.Value, IlExpressionCompilerHandlers.Conv_R4},
                {OpCodes.Conv_R8.Value, IlExpressionCompilerHandlers.Conv_R8},
                {OpCodes.Conv_U.Value, IlExpressionCompilerHandlers.Conv_U},
                {OpCodes.Conv_U1.Value, IlExpressionCompilerHandlers.Conv_U1},
                {OpCodes.Conv_U2.Value, IlExpressionCompilerHandlers.Conv_U2},
                {OpCodes.Conv_U4.Value, IlExpressionCompilerHandlers.Conv_U4},
                {OpCodes.Conv_U8.Value, IlExpressionCompilerHandlers.Conv_U8},
                {OpCodes.Conv_Ovf_I.Value, IlExpressionCompilerHandlers.Conv_Ovf_I},
                {OpCodes.Conv_Ovf_I1.Value, IlExpressionCompilerHandlers.Conv_Ovf_I1},
                {OpCodes.Conv_Ovf_I2.Value, IlExpressionCompilerHandlers.Conv_Ovf_I2},
                {OpCodes.Conv_Ovf_I4.Value, IlExpressionCompilerHandlers.Conv_Ovf_I4},
                {OpCodes.Conv_Ovf_I8.Value, IlExpressionCompilerHandlers.Conv_Ovf_I8},
                {OpCodes.Conv_Ovf_U.Value, IlExpressionCompilerHandlers.Conv_Ovf_U},
                {OpCodes.Conv_Ovf_U1.Value, IlExpressionCompilerHandlers.Conv_Ovf_U1},
                {OpCodes.Conv_Ovf_U2.Value, IlExpressionCompilerHandlers.Conv_Ovf_U2},
                {OpCodes.Conv_Ovf_U4.Value, IlExpressionCompilerHandlers.Conv_Ovf_U4},
                {OpCodes.Conv_Ovf_U8.Value, IlExpressionCompilerHandlers.Conv_Ovf_U8},
                {OpCodes.Conv_R_Un.Value, IlExpressionCompilerHandlers.Conv_R_Un},
                {OpCodes.Conv_Ovf_I1_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_I1_Un},
                {OpCodes.Conv_Ovf_I2_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_I2_Un},
                {OpCodes.Conv_Ovf_I4_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_I4_Un},
                {OpCodes.Conv_Ovf_I8_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_I8_Un},
                {OpCodes.Conv_Ovf_I_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_I_Un},
                {OpCodes.Conv_Ovf_U1_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_U1_Un},
                {OpCodes.Conv_Ovf_U2_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_U2_Un},
                {OpCodes.Conv_Ovf_U4_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_U4_Un},
                {OpCodes.Conv_Ovf_U8_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_U8_Un},
                {OpCodes.Conv_Ovf_U_Un.Value, IlExpressionCompilerHandlers.Conv_Ovf_U_Un},
                {OpCodes.Ldlen.Value, IlExpressionCompilerHandlers.Ldlen},
                {OpCodes.Ldfld.Value, IlExpressionCompilerHandlers.Ldfld},
                {OpCodes.Ldflda.Value, IlExpressionCompilerHandlers.Ldflda},
                {OpCodes.Ldsfld.Value, IlExpressionCompilerHandlers.Ldsfld},
                {OpCodes.Ldsflda.Value, IlExpressionCompilerHandlers.Ldsflda},
                {OpCodes.Stsfld.Value, IlExpressionCompilerHandlers.Stsfld},
                {OpCodes.Stfld.Value, IlExpressionCompilerHandlers.Stfld},
                {OpCodes.Ldftn.Value, IlExpressionCompilerHandlers.Ldftn},
                {OpCodes.Ldvirtftn.Value, IlExpressionCompilerHandlers.Ldvirtftn},
                {OpCodes.Pop.Value, IlExpressionCompilerHandlers.Pop},
                {OpCodes.Beq.Value, IlExpressionCompilerHandlers.Beq},
                {OpCodes.Beq_S.Value, IlExpressionCompilerHandlers.Beq_S},
                {OpCodes.Box.Value, IlExpressionCompilerHandlers.Box},
                {OpCodes.Unbox.Value, IlExpressionCompilerHandlers.Unbox},
                {OpCodes.Unbox_Any.Value, IlExpressionCompilerHandlers.Unbox_Any},
                {OpCodes.Isinst.Value, IlExpressionCompilerHandlers.Isinst},
                {OpCodes.Rem.Value, IlExpressionCompilerHandlers.Rem},
                {OpCodes.Rem_Un.Value, IlExpressionCompilerHandlers.Rem_Un},
                {OpCodes.And.Value, IlExpressionCompilerHandlers.And},
                {OpCodes.Or.Value, IlExpressionCompilerHandlers.Or},
                {OpCodes.Shl.Value, IlExpressionCompilerHandlers.Shl},
                {OpCodes.Shr.Value, IlExpressionCompilerHandlers.Shr},
                {OpCodes.Shr_Un.Value, IlExpressionCompilerHandlers.Shr_Un},
                {OpCodes.Leave.Value, IlExpressionCompilerHandlers.Leave},
                {OpCodes.Leave_S.Value, IlExpressionCompilerHandlers.Leave_S},
                {OpCodes.Endfinally.Value, IlExpressionCompilerHandlers.Endfinally},
                {OpCodes.Endfilter.Value, IlExpressionCompilerHandlers.Endfilter},
                {OpCodes.Throw.Value, IlExpressionCompilerHandlers.Throw},
                {OpCodes.Rethrow.Value, IlExpressionCompilerHandlers.Rethrow},
                {OpCodes.Constrained.Value, IlExpressionCompilerHandlers.Constrained},
                {OpCodes.Bgt.Value, IlExpressionCompilerHandlers.Bgt},
                {OpCodes.Bgt_S.Value, IlExpressionCompilerHandlers.Bgt_S},
                {OpCodes.Bgt_Un.Value, IlExpressionCompilerHandlers.Bgt_Un},
                {OpCodes.Bgt_Un_S.Value, IlExpressionCompilerHandlers.Bgt_Un_S},
                {OpCodes.Bge.Value, IlExpressionCompilerHandlers.Bge},
                {OpCodes.Bge_S.Value, IlExpressionCompilerHandlers.Bge_S},
                {OpCodes.Bge_Un.Value, IlExpressionCompilerHandlers.Bge_Un},
                {OpCodes.Bge_Un_S.Value, IlExpressionCompilerHandlers.Bge_Un_S},
                {OpCodes.Blt.Value, IlExpressionCompilerHandlers.Blt},
                {OpCodes.Blt_S.Value, IlExpressionCompilerHandlers.Blt_S},
                {OpCodes.Blt_Un.Value, IlExpressionCompilerHandlers.Blt_Un},
                {OpCodes.Blt_Un_S.Value, IlExpressionCompilerHandlers.Blt_Un_S},
                {OpCodes.Ldc_I4_M1.Value, IlExpressionCompilerHandlers.Ldc_I4_M1},
                {OpCodes.Castclass.Value, IlExpressionCompilerHandlers.Castclass},
                {OpCodes.Neg.Value, IlExpressionCompilerHandlers.Neg},
                {OpCodes.Mul.Value, IlExpressionCompilerHandlers.Mul},
                {OpCodes.Mul_Ovf.Value, IlExpressionCompilerHandlers.Mul_Ovf},
                {OpCodes.Mul_Ovf_Un.Value, IlExpressionCompilerHandlers.Mul_Ovf_Un},
                {OpCodes.Div.Value, IlExpressionCompilerHandlers.Div},
                {OpCodes.Div_Un.Value, IlExpressionCompilerHandlers.Div_Un},
                {OpCodes.Arglist.Value, IlExpressionCompilerHandlers.Arglist},
                {OpCodes.Ble.Value, IlExpressionCompilerHandlers.Ble},
                {OpCodes.Ble_S.Value, IlExpressionCompilerHandlers.Ble_S},
                {OpCodes.Ble_Un.Value, IlExpressionCompilerHandlers.Ble_Un},
                {OpCodes.Ble_Un_S.Value, IlExpressionCompilerHandlers.Ble_Un_S},
                {OpCodes.Bne_Un.Value, IlExpressionCompilerHandlers.Bne_Un},
                {OpCodes.Bne_Un_S.Value, IlExpressionCompilerHandlers.Bne_Un_S},
                {OpCodes.Break.Value, IlExpressionCompilerHandlers.Break},
                {OpCodes.Calli.Value, IlExpressionCompilerHandlers.Calli},
                {OpCodes.Cpblk.Value, IlExpressionCompilerHandlers.Cpblk},
                {OpCodes.Cpobj.Value, IlExpressionCompilerHandlers.Cpobj},
                {OpCodes.Initblk.Value, IlExpressionCompilerHandlers.Initblk},
                {OpCodes.Jmp.Value, IlExpressionCompilerHandlers.Jmp},
                {OpCodes.Ldc_I8.Value, IlExpressionCompilerHandlers.Ldc_I8},
                {OpCodes.Ldind_I.Value, IlExpressionCompilerHandlers.Ldind_I},
                {OpCodes.Ldind_I1.Value, IlExpressionCompilerHandlers.Ldind_I1},
                {OpCodes.Ldind_I2.Value, IlExpressionCompilerHandlers.Ldind_I2},
                {OpCodes.Ldind_I4.Value, IlExpressionCompilerHandlers.Ldind_I4},
                {OpCodes.Ldind_I8.Value, IlExpressionCompilerHandlers.Ldind_I8},
                {OpCodes.Ldind_R4.Value, IlExpressionCompilerHandlers.Ldind_R4},
                {OpCodes.Ldind_R8.Value, IlExpressionCompilerHandlers.Ldind_R8},
                {OpCodes.Ldind_Ref.Value, IlExpressionCompilerHandlers.Ldind_Ref},
                {OpCodes.Ldind_U1.Value, IlExpressionCompilerHandlers.Ldind_U1},
                {OpCodes.Ldind_U2.Value, IlExpressionCompilerHandlers.Ldind_U2},
                {OpCodes.Ldind_U4.Value, IlExpressionCompilerHandlers.Ldind_U4},
                {OpCodes.Ldobj.Value, IlExpressionCompilerHandlers.Ldobj},
                {OpCodes.Localloc.Value, IlExpressionCompilerHandlers.Localloc},
                {OpCodes.Mkrefany.Value, IlExpressionCompilerHandlers.Mkrefany},
                {OpCodes.Not.Value, IlExpressionCompilerHandlers.Not},
                {OpCodes.Prefix1.Value, IlExpressionCompilerHandlers.Prefix1},
                {OpCodes.Prefix2.Value, IlExpressionCompilerHandlers.Prefix2},
                {OpCodes.Prefix3.Value, IlExpressionCompilerHandlers.Prefix3},
                {OpCodes.Prefix4.Value, IlExpressionCompilerHandlers.Prefix4},
                {OpCodes.Prefix5.Value, IlExpressionCompilerHandlers.Prefix5},
                {OpCodes.Prefix6.Value, IlExpressionCompilerHandlers.Prefix6},
                {OpCodes.Prefix7.Value, IlExpressionCompilerHandlers.Prefix7},
                {OpCodes.Prefixref.Value, IlExpressionCompilerHandlers.Prefixref},
                {OpCodes.Readonly.Value, IlExpressionCompilerHandlers.Readonly},
                {OpCodes.Refanytype.Value, IlExpressionCompilerHandlers.Refanytype},
                {OpCodes.Refanyval.Value, IlExpressionCompilerHandlers.Refanyval},
                {OpCodes.Sizeof.Value, IlExpressionCompilerHandlers.Sizeof},
                {OpCodes.Starg.Value, IlExpressionCompilerHandlers.Starg},
                {OpCodes.Starg_S.Value, IlExpressionCompilerHandlers.Starg_S},
                {OpCodes.Stind_I.Value, IlExpressionCompilerHandlers.Stind_I},
                {OpCodes.Stind_I1.Value, IlExpressionCompilerHandlers.Stind_I1},
                {OpCodes.Stind_I2.Value, IlExpressionCompilerHandlers.Stind_I2},
                {OpCodes.Stind_I4.Value, IlExpressionCompilerHandlers.Stind_I4},
                {OpCodes.Stind_I8.Value, IlExpressionCompilerHandlers.Stind_I8},
                {OpCodes.Stind_R4.Value, IlExpressionCompilerHandlers.Stind_R4},
                {OpCodes.Stind_R8.Value, IlExpressionCompilerHandlers.Stind_R8},
                {OpCodes.Stind_Ref.Value, IlExpressionCompilerHandlers.Stind_Ref},
                {OpCodes.Stobj.Value, IlExpressionCompilerHandlers.Stobj},
                {OpCodes.Tailcall.Value, IlExpressionCompilerHandlers.Tailcall},
                {OpCodes.Unaligned.Value, IlExpressionCompilerHandlers.Unaligned},
                {OpCodes.Volatile.Value, IlExpressionCompilerHandlers.Volatile},
                {OpCodes.Xor.Value, IlExpressionCompilerHandlers.Xor},
                {OpCodes.Ckfinite.Value, IlExpressionCompilerHandlers.Ckfinite},
            };

            // var allOpCodes = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode)).OrderBy(f => f.Name);
            // var missingOpCodes = allOpCodes.Select(f => (OpCode)f.GetValue(null)).Where(op => !handlers.ContainsKey(op.Value)).Select(o => o.Name).ToArray();
            // Debug.WriteLine("Missing OpCode handlers: " + string.Join(",",missingOpCodes));
            // Console.WriteLine("Missing OpCode handlers: " + string.Join(",",missingOpCodes));
            return handlers;
        }
    }
}