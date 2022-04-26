using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;

namespace LiveSharp.Runtime.IL
{
    [DebuggerDisplay("#{Index} {OpCode} {Operand} {Comment} {Debug}")]
    public class IlInstruction
    {
        public OpCode OpCode { get; set; }
        public object Operand { get; set; }
        public string Comment => _comment != null ? ("//" + _comment) : string.Empty;
        public int Index => _parentList.GetInstructionIndex(this);

        public IlInstruction Previous { get; set; }
        public IlInstruction Next { get; set; }
        public string Debug { get; set; }

        private readonly IlInstructionList _parentList;
        private readonly string _comment;

        public IlInstruction(OpCode opCode, object operand, IlInstructionList parentList, string comment = null)
        {
            _parentList = parentList;
            OpCode = opCode;
            Operand = operand;
            _comment = comment;
        }

        public IlInstruction Prepend(IlInstruction instruction)
        {
            _parentList.InsertBefore(this, instruction);
            return instruction;
        }

        public IlInstruction Append(IlInstruction instruction)
        {
            _parentList.InsertAfter(this, instruction);
            return instruction;
        }

        public void ReplaceWith(IlInstruction instruction)
        {
            _parentList.ReplaceWith(this, instruction);
        }

        public void ReplaceWith(IReadOnlyList<IlInstruction> instructions)
        {
            _parentList.ReplaceWith(this, instructions);
        }
        
        public override string ToString()
        {
            return $"{OpCode} {Operand} {Comment}";
        }
    }
}