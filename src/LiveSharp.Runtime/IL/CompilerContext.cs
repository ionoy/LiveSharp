using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Virtual;
using LiveSharp.Shared;
using System.Reflection;

namespace LiveSharp.Runtime.IL
{
    public class CompilerContext
    {
        public List<ParameterExpression> IdleRegisters { get; } = new List<ParameterExpression>();
        public IReadOnlyList<Expression> Locals { get; }
        public Dictionary<LocalMetadata, ParameterExpression> LocalMap { get; }
        public VirtualMethodInfo Method { get; }
        public Expression CastedThis { get; }
        public Dictionary<int, LabelTarget> LabelTargets { get; } = new Dictionary<int, LabelTarget>();
        public List<ParameterExpression> Registers { get; private set; } = new List<ParameterExpression>();
        
        private List<Expression> _arguments { get; }
        private int _registerCounter;
        private readonly IReadOnlyList<IlInstruction> _instructions;
        private readonly Expression[] _expressions;
        private readonly ImmutableStack<Expression>[] _stacks;
        private readonly List<IlJump>[] _jumps;
        private readonly ConcurrentDictionary<int, List<int>> _stackSlotUsageLocations = new();
        private readonly ConcurrentDictionary<int, List<int>> _mergedStacks = new();
        private readonly List<StackSlotExpression> _stackSlots = new();
        private readonly ConcurrentDictionary<int, StackSlotExpression> _stackSlotsByInstruction = new();
        private readonly Stack<IlInstruction> _branchesToCompile = new();
        private readonly VirtualMethodBody _methodBody;
        private readonly Func<InstructionContext, Expression> _compileInstruction;
        private readonly ILogger _logger;

        private static readonly HashSet<int> PushSelfInsteadOfRegister = new() {
            OpCodes.Ldloca.Value, OpCodes.Ldloca_S.Value,
            OpCodes.Ldelema.Value,
            OpCodes.Ldflda.Value,
            OpCodes.Dup.Value,
        };

        public CompilerContext(VirtualMethodInfo methodInfo,
            VirtualMethodBody methodBody,
            IReadOnlyList<ParameterExpression> arguments,
            IReadOnlyList<Expression> locals,
            Dictionary<LocalMetadata, ParameterExpression> localMap,
            Func<InstructionContext, Expression> compileInstruction,
            ILogger logger)
        {
            Locals = locals;
            LocalMap = localMap;
            Method = methodInfo;

            _arguments = arguments.OfType<Expression>().ToList();
            _instructions = methodBody.Instructions.ToArray();
            _expressions = new Expression[_instructions.Count];
            _stacks = new ImmutableStack<Expression>[_instructions.Count];
            _jumps = new List<IlJump>[_instructions.Count];
            _methodBody = methodBody;
            _compileInstruction = compileInstruction;
            _logger = logger;

            _stacks[0] = ImmutableStack.Empty<Expression>();

            if (!methodInfo.IsStatic)
                CastedThis = Locals[Locals.Count - 1];
        }

        public IReadOnlyList<Expression> CompileInstructions()
        {
            compileBranch(0);

            while (_branchesToCompile.Count > 0) {
                var branch = _branchesToCompile.Pop();
                compileBranch(branch.Index);
            }

            // for (int i = 0; i < _instructions.Count; i++) {
            //     var instruction = _instructions[i];
            //     var expression = _expressions[i];
            //
            //     Debug.WriteLine($"{instruction.Index}: {instruction.OpCode} {instruction.Operand} --> {expression}");
            // }

            var compilerRegisterBuilder = new CompilerRegisterBuilder(_stackSlots, _stackSlotUsageLocations, _mergedStacks);
            var registerSubstitutes = compilerRegisterBuilder.BuildRegistersFromStackSlots();

            SubstituteRegisters(registerSubstitutes);

            Registers = compilerRegisterBuilder.Registers;
            
            void compileBranch(int index)
            {
                var nextInstruction = CompileBranch(index);
                while (nextInstruction != -1) 
                    nextInstruction = CompileBranch(nextInstruction);
            }
            
            return _expressions;
        }

        public void SubstituteRegisters(ConcurrentDictionary<int, ParameterExpression> registerSubstitutes)
        {
            var visitor = new StackSlotSubstituteVisitor(registerSubstitutes);
            for (int i = 0; i < _expressions.Length; i++) {
                _expressions[i] = visitor.Visit(_expressions[i]);
            }
        }

        public Expression GetArgumentByIndex(int index)
        {
            return _arguments[index];
        }

        private int CompileBranch(int instructionIndex)
        {
            if (instructionIndex > _instructions.Count - 1)
                return -1;

            var oldStack = _stacks[instructionIndex];

            if (_expressions[instructionIndex] != null)
                return -1;

            if (oldStack == null)
                throw new Exception("Uninitialized stack at index " + instructionIndex);

            foreach (var stackExpression in oldStack.Distinct()) {
                if (stackExpression is StackSlotExpression stackSlot)
                    _stackSlotUsageLocations.GetOrAdd(stackSlot.InstructionIndex, _ => new List<int>()).Add(instructionIndex);

                if (stackExpression is BinaryExpression binary && binary.NodeType == ExpressionType.ArrayIndex) {
                    if (binary.Right is StackSlotExpression indexer)
                        _stackSlotUsageLocations.GetOrAdd(indexer.InstructionIndex, _ => new List<int>()).Add(instructionIndex);
                }
            }

            var instruction = _instructions[instructionIndex];
            var (expression, updatedStack) = Compile(instruction, oldStack);
            
            _expressions[instruction.Index] = expression;

            var optionalJumps = _jumps[instructionIndex];
            
            if (optionalJumps != null) {
                // We don't pass stack with Leave
                // if (optionalJump.IsExceptionBlockLeave)
                //     return optionalJump.To;
                foreach (var optionalJump in optionalJumps) {
                    PassStack(optionalJump.To, updatedStack);

                    if (optionalJump.IsConditional) {
                        _branchesToCompile.Push(optionalJump.To);
                    } else {
                        // Continue compiling from the jump location 
                        return optionalJump.To.Index;
                    }
                }
            }

            var nextInstructionIndex = instructionIndex + 1;

            var reachedExceptionBlockEnd = IsEndOfExceptionBlock(nextInstructionIndex);
            if (reachedExceptionBlockEnd) {
                // protected block ended without Leave (most likely with throw)
                // stop current branch compilation here
                return -1;
            }
            
            if (_instructions.Count > nextInstructionIndex)
                PassStack(_instructions[nextInstructionIndex], updatedStack);

            var tryBlockFound = false;
            foreach (var tryBlockInfo in _methodBody.TryBlocks) {
                if (tryBlockInfo.Start.Index == nextInstructionIndex) {
                    HandleTryBlock(tryBlockInfo, updatedStack);
                    tryBlockFound = true;
                }
            }
            
            if (tryBlockFound)
                return -1;

            return nextInstructionIndex;
        }

        private void HandleTryBlock(TryBlockInfo tryBlock, ImmutableStack<Expression> updatedStack)
        {
            // Try block starts next

            // Try block should be processed after all the handlers
            var tryBlockEnd = tryBlock.HandlerEnd;
            if (!IsStartOfHandlerBlock(tryBlockEnd)) {
                // we don't need to pass stack because it will either clear with "leave" or continue from try {} 
                //PassStack(tryBlockEnd, stack);
                _branchesToCompile.Push(tryBlockEnd);
            }

            // We need to pass stack to all handlers and to the end of the block
            // Then add branches for every handler and the end of the block
            
            if (tryBlock.HandlerType == "Catch") {
                var catchType = tryBlock.CatchType;
                tryBlock.ExceptionParameter = Expression.Parameter(catchType, "e");

                PassStack(tryBlock.HandlerStart, updatedStack.Push(tryBlock.ExceptionParameter));
                _branchesToCompile.Push(tryBlock.HandlerStart);
            }
            else if (tryBlock.HandlerType == "Finally") {
                PassStack(tryBlock.HandlerStart, updatedStack);
                _branchesToCompile.Push(tryBlock.HandlerStart);
            }
            else if (tryBlock.HandlerType == "Filter") {
                var catchType = tryBlock.CatchType ?? typeof(object);

                tryBlock.ExceptionParameter = Expression.Parameter(catchType, "e");

                var stackWithException = updatedStack.Push(tryBlock.ExceptionParameter);

                PassStack(tryBlock.FilterStart, stackWithException);
                PassStack(tryBlock.HandlerStart, stackWithException);

                _branchesToCompile.Push(tryBlock.FilterStart);
                _branchesToCompile.Push(tryBlock.HandlerStart);
            }
            else {
                throw new NotImplementedException($"Unsupported exception handler {tryBlock.HandlerType}");
            }

            PassStack(tryBlock.Start, updatedStack);
            
            _branchesToCompile.Push(tryBlock.Start);
        }

        private bool IsEndOfExceptionBlock(int instructionIndex)
        {
            foreach (var tryBlock in _methodBody.TryBlocks) {
                if (tryBlock.End?.Index == instructionIndex)
                    return true;
                
                var isEndOfFilter = tryBlock.FilterStart?.Index != -1 && instructionIndex == tryBlock.HandlerStart?.Index;
                if (tryBlock.HandlerEnd?.Index == instructionIndex || isEndOfFilter)
                    return true;
            }

            return false;
        }

        private bool IsStartOfHandlerBlock(IlInstruction instruction)
        {
            foreach (var tryBlock in _methodBody.TryBlocks)
                if (tryBlock.HandlerStart == instruction)
                    return true;

            return false;
        }
        
        private void PassStack(IlInstruction instruction, ImmutableStack<Expression> stack)
        {
            if (_stacks[instruction.Index] != null) {
                // Stack layouts should be identical at any given location (as per ECMA-335)
                var originalStack = _stacks[instruction.Index].OfType<StackSlotExpression>().ToArray();
                var passedStack = stack.OfType<StackSlotExpression>().ToArray();

                for (int i = 0; i < originalStack.Length; i++) {
                    var originalStackSlot = originalStack[i];
                    var passedStackSlot = passedStack[i];
                    
                    if (originalStackSlot.InstructionIndex == passedStackSlot.InstructionIndex)
                        continue;
                    
                    var mergedStackSlots = _mergedStacks.GetOrAdd(originalStackSlot.InstructionIndex, _ => new List<int>());
                    mergedStackSlots.Add(passedStackSlot.InstructionIndex);

                    ResolveNulls(originalStackSlot, mergedStackSlots);
                }
            }
            
            _stacks[instruction.Index] = stack;
        }

        private void ResolveNulls(StackSlotExpression originalStackSlot, List<int> mergedStackSlots)
        {
            var allSlots = new[] { originalStackSlot }.Concat(mergedStackSlots.Select(index => _stackSlotsByInstruction[index])).ToArray();
            var nonObjectSlot = allSlots.FirstOrDefault(s => s.Type != typeof(object));

            if (nonObjectSlot != null) {
                var nullSlots = allSlots.Where(s => s.IsNull);
                
                foreach (var nullSlot in nullSlots) {
                    nullSlot.ChangeType(nonObjectSlot.Type);
                }
            }
        }

        private (Expression expression, ImmutableStack<Expression> stack) Compile(IlInstruction instruction, ImmutableStack<Expression> stack)
        {
            var instructionContext = new InstructionContext(instruction, this, stack);
            var expression = _compileInstruction(instructionContext);
            var resultMetadata = instructionContext.ResultMetadata;
            
            stack = instructionContext.GetStack();

            if (instruction.OpCode.StackBehaviourPush != StackBehaviour.Push0 && expression.Type != typeof(void)) {
                if (PushSelfInsteadOfRegister.Contains(instruction.OpCode.Value) || isVirtualClrLdflda(expression)) {
                    stack = stack.Push(expression, resultMetadata);
                } else if (expression is ParameterExpression { IsByRef: true }) {
                    stack = stack.Push(expression, resultMetadata);
                } else {
                    var isNull = instruction.OpCode.Value == OpCodes.Ldnull.Value;
                    var register = CreateStackSlot(instruction, expression, isNull);
                    
                    expression = Expression.Assign(register, CompilerHelpers.Coerce(expression, register.Type));

                    stack = stack.Push(register, resultMetadata);
                }
            }
            
            return (expression, stack);

            static bool isVirtualClrLdflda(Expression expression)
            {
                if (expression is MethodCallExpression mc) {
                    var method = mc.Method;
                    if (method.DeclaringType == typeof(VirtualClr)) {
                        return method.Name == "Ldflda";
                    }
                }
                return false;
            }
        }

        private StackSlotExpression CreateStackSlot(IlInstruction instruction, Expression expression, bool isNull)
        {
            var expressionType = CompilerHelpers.ResolveVirtualType(expression.Type);
            var stackSlot = new StackSlotExpression(expressionType, $"$r{_registerCounter++}_{expression.Type.Name}", instruction.Index, isNull);
            
            _stackSlots.Add(stackSlot);
            _stackSlotsByInstruction[stackSlot.InstructionIndex] = stackSlot;
            
            return stackSlot;
        }

        public LabelTarget GetOrCreateLabelTarget(int from, int to, bool isConditional, bool isExceptionBlockLeave = false)
        {
            var jumps = _jumps[from];

            if (jumps == null)
                jumps = _jumps[from] = new List<IlJump>();
            
            jumps.Add(new IlJump(_instructions[to], isConditional, isExceptionBlockLeave));

            if (LabelTargets.TryGetValue(to, out var target))
                return target;

            return LabelTargets[to] = Expression.Label(to.ToString());
        }

        class IlJump
        {
            public IlInstruction To { get; }
            public bool IsConditional { get; }
            public bool IsExceptionBlockLeave { get; }

            public IlJump(IlInstruction to, bool isConditional, bool isExceptionBlockLeave)
            {
                To = to;
                IsConditional = isConditional;
                IsExceptionBlockLeave = isExceptionBlockLeave;
            }
        }
    }
}