using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;

namespace LiveSharp.Runtime.IL
{
    public class IlExpression
    {
        public Expression Expression { get; }
        public IlInstruction IlInstruction { get; }
        public bool HasStackValue => IlInstruction.OpCode.StackBehaviourPush != StackBehaviour.Pop0;
        
        public IlExpression(IlInstruction ilInstruction, Expression expression)
        {
            Expression = expression;
            IlInstruction = ilInstruction;
        }
    }
}