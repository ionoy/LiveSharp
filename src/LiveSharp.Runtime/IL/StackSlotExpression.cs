using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace LiveSharp.Runtime.IL
{
    public class StackSlotExpression : Expression
    {
        public string Name { get; }

        public override Type Type => _type;

        public override bool CanReduce => true;
        public override ExpressionType NodeType => ExpressionType.Parameter;
        public int InstructionIndex { get; }
        public bool IsNull { get; }
        public Type SpeculativeTargetType { get; set; }

        private Type _type;
        private Type _originalType;

        public StackSlotExpression(Type type, string name, int instructionIndex, bool isNull)
        {
            Name = name;
            InstructionIndex = instructionIndex;
            IsNull = isNull;
            
            _type = type;
            _originalType = type;
        }
        
        public void ChangeType(Type targetType)
        {
            _type = targetType;
        }

        public override string ToString() => Name + $" ({_type.Name})";
    }
}