using System.Linq.Expressions;
using LiveSharp.Runtime.Infrastructure;

namespace LiveSharp.Runtime.IL
{
    public class InstructionContext
    {
        public IlInstruction Instruction { get; }
        public CompilerContext Compiler { get; }
        public object ResultMetadata { get; set; }

        private ImmutableStack<Expression> _stack;

        public InstructionContext(IlInstruction instruction, CompilerContext compiler, ImmutableStack<Expression> stack)
        {
            Instruction = instruction;
            Compiler = compiler;
            
            _stack = stack;
        }

        public Expression PopStack()
        {
            var (newStack, value, _) = _stack.Pop();
            _stack = newStack;
            return value;
        }

        public (Expression value, object metadata) PopStackWithMetadata()
        {
            var (newStack, value, metadata) = _stack.Pop();
            _stack = newStack;
            return (value, metadata);
        }

        public Expression PeekStack()
        {
            return _stack.Peek();
        }

        public ImmutableStack<Expression> GetStack() => _stack;

        public void EmptyStack()
        {
            _stack = ImmutableStack.Empty<Expression>();
        }
    }
}