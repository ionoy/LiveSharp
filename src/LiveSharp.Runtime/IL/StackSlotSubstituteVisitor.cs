using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace LiveSharp.Runtime.IL
{
    class StackSlotSubstituteVisitor : ExpressionVisitor
    {
        private readonly ConcurrentDictionary<int, ParameterExpression> _stackSlotParameterSubstitutes;

        public StackSlotSubstituteVisitor(ConcurrentDictionary<int, ParameterExpression> stackSlotParameterSubstitutes)
        {
            _stackSlotParameterSubstitutes = stackSlotParameterSubstitutes;
        }

        public override Expression Visit(Expression node)
        {
            // Some merged stacks actually have different register types in them
            // like Int32 <--> Boolean
            // These types are basically the same from the stack machine POV
            // but we need to account for them after substitutions
            // I obviously need to find a better way to handle it
            if (node is BinaryExpression binary && binary.NodeType == ExpressionType.Assign) {
                if (binary.Left is StackSlotExpression s && _stackSlotParameterSubstitutes.TryGetValue(s.InstructionIndex, out var p)) {
                    var right = Visit(binary.Right);
                    return Expression.Assign(p, CompilerHelpers.Coerce(right, p.Type));
                }
            }
            
            if (node is StackSlotExpression stackSlot && _stackSlotParameterSubstitutes.TryGetValue(stackSlot.InstructionIndex, out var parameter))
                return parameter;
            
            return base.Visit(node);
        }
    }
}