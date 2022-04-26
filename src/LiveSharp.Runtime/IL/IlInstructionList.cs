using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace LiveSharp.Runtime.IL
{
    public class IlInstructionList : IEnumerable<IlInstruction>
    {
        public IlInstruction Head { get; private set; }
        public IlInstruction Tail { get; private set; }
        public TryBlockInfo[] TryBlocks { get; private set; } = new TryBlockInfo[0];
        public List<LocalMetadata> Locals { get; }

        private readonly ConcurrentDictionary<IlInstruction, int> _indices = new();
        private List<(IlInstruction from, IlInstruction to)> _jumpTargets = new ();
        private bool _indicesDirty = true;

        public IlInstructionList(List<LocalMetadata> locals)
        {
            Locals = locals;
        }
        
        public void Append(OpCode opCode, object operand, string comment = null)
        {
            var newInstruction = new IlInstruction(opCode, operand, this, comment);
            
            if (Head == null) {
                Head = newInstruction;
                Tail = newInstruction;
            } else {
                newInstruction.Previous = Tail;
                Tail.Next = newInstruction;
                Tail = newInstruction;
            }
            
            _indicesDirty = true;
        }

        public void InsertBefore(IlInstruction targetInstruction, IlInstruction instructionToInsert)
        {
            if (targetInstruction.Previous != null) {
                instructionToInsert.Previous = targetInstruction.Previous;
                targetInstruction.Previous.Next = instructionToInsert;
            } else {
                Head = instructionToInsert;
            }
            
            targetInstruction.Previous = instructionToInsert;
            instructionToInsert.Next = targetInstruction;
            
            _indicesDirty = true;
        }

        public void InsertAfter(IlInstruction targetInstruction, IlInstruction instructionToInsert)
        {
            if (targetInstruction.Next != null) {
                instructionToInsert.Next = targetInstruction.Next;
                targetInstruction.Next.Previous = instructionToInsert;
            } else {
                Tail = instructionToInsert;
            }
            
            targetInstruction.Next = instructionToInsert;
            instructionToInsert.Previous = targetInstruction;
            
            _indicesDirty = true;
        }

        public void ReplaceWith(IlInstruction targetInstruction, IlInstruction newInstruction)
        {
            var prev = targetInstruction.Previous;
            var next = targetInstruction.Next;

            if (prev != null) 
                prev.Next = newInstruction;

            if (next != null) 
                next.Previous = newInstruction;

            newInstruction.Next = next;

            if (targetInstruction == Head) 
                Head = newInstruction;

            if (targetInstruction == Tail)
                Tail = newInstruction;

            UpdateJumpTargets(targetInstruction, newInstruction);

            _indicesDirty = true;
        }
        
        public void ReplaceWith(IlInstruction oldInstruction, IReadOnlyList<IlInstruction> newInstructions)
        {
            if (newInstructions.Count == 0)
                throw new InvalidOperationException("Cannot use ReplaceWith with an empty collection");
                
            var prev = oldInstruction.Previous;
            var next = oldInstruction.Next;

            // remove current instruction from list
            if (prev != null)
                prev.Next = next;
            
            if (next != null)
                next.Previous = prev;
            
            // append new list
            if (next != null) {
                foreach (var instruction in newInstructions)
                    next.Prepend(instruction);
            } else if (prev != null) {
                foreach (var instruction in newInstructions) {
                    prev.Append(instruction);
                    prev = instruction;
                }
            }
            
            UpdateJumpTargets(oldInstruction, newInstructions[0]);
        }

        private void UpdateJumpTargets(IlInstruction old, IlInstruction @new)
        {
            for (int i = 0; i < _jumpTargets.Count; i++) {
                var (from, to) = _jumpTargets[i];
                if (to == old) {
                    from.Operand = @new;
                    _jumpTargets[i] = (from, @new);
                }
            }

            foreach (var tryBlock in TryBlocks) {
                if (tryBlock.Start == old)
                    tryBlock.Start = @new;
                if (tryBlock.End == old)
                    tryBlock.End = @new;
                if (tryBlock.HandlerStart == old)
                    tryBlock.HandlerStart = @new;
                if (tryBlock.HandlerEnd == old)
                    tryBlock.HandlerEnd = @new;
                if (tryBlock.FilterStart == old)
                    tryBlock.FilterStart = @new;
            }
        }
        
        public void AddJumpTarget(IlInstruction from, IlInstruction to)
        {
            _jumpTargets.Add((from, to));
        }

        public int GetInstructionIndex(IlInstruction ilInstruction)
        {
            if (_indicesDirty) {
                int index = 0;
                
                _indices.Clear();
                
                foreach (var instruction in this)
                    _indices[instruction] = index++;
                
                _indicesDirty = false;
            }

            if (_indices.TryGetValue(ilInstruction, out var i))
                return i;

            throw new InvalidOperationException($"Instruction {ilInstruction} doesn't belong to this list");
        }
        
        public IlInstructionList Clone()
        {
            var newTryBlocks = TryBlocks.Select(t => t.ToTryBlock()).ToArray();
            var newLocals = Locals.Select(l => new LocalMetadata(l.LocalName, l.LocalType)).ToList();
            var newInstructionList = new IlInstructionList(newLocals);
            var oldInstructions = this.ToArray();
            
            foreach (var instruction in oldInstructions) {
                var opCode = instruction.OpCode;
                var operand = instruction.Operand;

                newInstructionList.Append(opCode, operand, instruction.Comment);
            }

            var newInstructions = newInstructionList.ToArray();
            foreach (var newInstruction in newInstructions) {
                if (newInstruction.Operand is LocalMetadata local)
                    newInstruction.Operand = newLocals[Locals.IndexOf(local)];
                
                if (newInstruction.Operand is IlInstruction i)
                    newInstruction.Operand = newInstructions[i.Index];
            }

            newInstructionList.AddTryBlocks(newTryBlocks);
            newInstructionList._jumpTargets = _jumpTargets.Select(t => (newInstructions[t.from.Index], newInstructions[t.to.Index])).ToList();
            
            return newInstructionList;
        }
        
        public void AddTryBlocks(TryBlock[] tryBlocks)
        {
            var instructionArray = this.ToArray();
            var tryBlockInfos = tryBlocks.Select(t => t.ToTryBlockInfo(instructionArray));
            
            TryBlocks = TryBlocks.Concat(tryBlockInfos).ToArray();
        }

        public IEnumerator<IlInstruction> GetEnumerator()
        {
            return new IlInstructionEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class IlInstructionEnumerator : IEnumerator<IlInstruction>
        {
            private readonly IlInstructionList _list;
            private IlInstruction _current;
            public IlInstructionEnumerator(IlInstructionList list)
            {
                _list = list;
            }
            
            public bool MoveNext()
            {
                if (_current == null) {
                    _current = _list.Head;
                    return _current != null;
                }

                if (_current.Next == null)
                    return false;
                
                _current = _current.Next;
                
                return true;
            }

            public void Reset()
            {
                _current = null;
            }

            public IlInstruction Current => _current;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                _current = null;
            }
        }
    }
}