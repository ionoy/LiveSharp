#if NETSTANDARD2_1
using LiveSharp.Runtime.Infrastructure;
using LiveSharp.Runtime.Virtual;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
namespace LiveSharp.Runtime.IL
{
    public class IlDynamicMethodCompiler
    {
        private readonly DelegateBuilder _delegateBuilder;
        private readonly VirtualMethodBody _methodBody;
        private readonly IlInstructionList _instructionList;
        private int _maxMidStackCur;
        private int _maxMidStack;
        private int _maxStackSize;
        private readonly Dictionary<IlInstruction, int> _instructionOffsets = new();
        private readonly List<LabelFixup> _fixups = new();
        private DynamicILInfo _dynamicIlInfoDebug;

        public IlDynamicMethodCompiler(DelegateBuilder delegateBuilder, VirtualMethodBody methodBody, IlInstructionList instructionList, ILogger logger)
        {
            _delegateBuilder = delegateBuilder;
            _methodBody = methodBody;
            _instructionList = instructionList;
        }
        
        public Delegate GetDelegate(Type delegateType)
        {
            var dynamicMethod = CreateDynamicMethod();
            return dynamicMethod.CreateDelegate(delegateType);
        }

        DynamicMethod CreateDynamicMethod() 
        {
            var parameterTypes = _delegateBuilder.Parameters.Select(p => CompilerHelpers.ResolveVirtualType(p.ParameterType)).ToList();

            if (!_delegateBuilder.IsStatic) 
                parameterTypes.Insert(0, typeof(object));

            var method = new DynamicMethod(_delegateBuilder.Name, CompilerHelpers.ResolveVirtualType(_delegateBuilder.MethodInfo.ReturnType), parameterTypes.ToArray(), CompilerHelpers.ResolveVirtualType(_delegateBuilder.DeclaringType));
            var ilInfo = method.GetDynamicILInfo();
            
            SetLocalSignature(ilInfo);
            SetCode(ilInfo);
            SetExceptions(ilInfo);

            _dynamicIlInfoDebug = ilInfo;
            
            return method;
        }

        void Emit(DynamicILInfo ilInfo, IlInstruction instruction, BinaryWriter writer)
        {
            var opCode = instruction.OpCode;
            
            if (opCode.Size == 1) {
                writer.Write((byte)opCode.Value);
            } else {
                var b0 = (byte) ((opCode.Value >> 8) & 0xff); 
                var b1 = (byte) ((opCode.Value) & 0xff); 
                writer.Write(b0);
                writer.Write(b1);
            }

            EmitOperand(ilInfo, instruction, writer);
        }

        private void EmitOperand(DynamicILInfo ilInfo, IlInstruction instruction, BinaryWriter writer)
        {
            var operand = instruction.Operand;

            switch (instruction.OpCode.OperandType) {
                case OperandType.InlineBrTarget:
                    var target = (IlInstruction)operand;
                    AddFixup((int) writer.BaseStream.Position + 4, writer, target);
                    break;
                case OperandType.ShortInlineBrTarget:
                    var shortTarget = (IlInstruction)operand;
                    AddFixup((int) writer.BaseStream.Position + 1, writer, shortTarget, true);
                    break;
                case OperandType.InlineField:
                    var fi = (FieldInfo)operand;
                    writer.Write(GetFieldToken(ilInfo, fi));
                    break;
                case OperandType.InlineI:
                    var integer = (int)operand;
                    writer.Write(integer);
                    break;
                case OperandType.ShortInlineI:
                    var shortI = Convert.ToSByte((int)operand);
                    writer.Write(shortI);
                    break;
                case OperandType.InlineI8:
                    throw new NotImplementedException(nameof(OperandType.InlineI8));
                case OperandType.InlineMethod:
                    var methodInfo = (MethodBase)operand;
                    var methodToken = GetMethodToken(ilInfo, methodInfo);

                    writer.Write(methodToken);
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.InlinePhi:
                    throw new NotImplementedException(nameof(OperandType.InlinePhi));
                case OperandType.InlineR:
                    var dbl = (double)operand;
                    writer.Write(dbl);
                    break;
                case OperandType.ShortInlineR:
                    var flt = (float)operand;
                    writer.Write(flt);
                    break;
                case OperandType.InlineSig:
                    throw new NotImplementedException(nameof(OperandType.InlineSig));
                case OperandType.InlineString:
                    var str = (string)operand;
                    var stringToken = ilInfo.GetTokenFor(str);
                    writer.Write(stringToken);
                    break;
                case OperandType.InlineSwitch:
                    var targets = (IlInstruction[])operand;
                    writer.Write(targets.Length);
                    var offsetSource = (int)writer.BaseStream.Position + targets.Length * 4;
                    foreach (var switchTarget in targets) 
                        AddFixup(offsetSource, writer, switchTarget);
                    break;
                case OperandType.InlineTok:
                    if (operand is Type t) {
                        writer.Write(ilInfo.GetTokenFor(CompilerHelpers.ResolveVirtualType(t).TypeHandle));
                    } else if (operand is FieldInfo fieldInfo) {
                        writer.Write(GetFieldToken(ilInfo, fieldInfo));
                    }  else if (operand is MethodInfo method) {
                        writer.Write(GetMethodToken(ilInfo, method));
                    } else { 
                        throw new NotImplementedException(nameof(OperandType.InlineTok) + " " + instruction);
                    }
                    break;
                case OperandType.InlineType:
                    var type = (Type)operand;
                    writer.Write(ilInfo.GetTokenFor(CompilerHelpers.ResolveVirtualType(type).TypeHandle));
                    break;
                case OperandType.InlineVar:
                    if (operand is LocalMetadata local) {
                        writer.Write((short)_methodBody.Locals.IndexOf(local));
                    } else if (operand is int intLocal) {
                        writer.Write((short)intLocal);
                    }
                    break;
                case OperandType.ShortInlineVar:
                    if (operand is LocalMetadata shortLocal) {
                        writer.Write((byte)_methodBody.Locals.IndexOf(shortLocal));
                    } else if (operand is int intLocal) {
                        writer.Write((byte)intLocal);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static int GetFieldToken(DynamicILInfo ilInfo, FieldInfo fieldInfo)
        {
            var declaringType = fieldInfo.DeclaringType?.ResolveVirtualType() ?? throw new InvalidOperationException($"Field {fieldInfo} declaring type is null");
            return declaringType.IsGenericType
                ? ilInfo.GetTokenFor(fieldInfo.FieldHandle, declaringType.TypeHandle)
                : ilInfo.GetTokenFor(fieldInfo.FieldHandle);
        }

        private static int GetMethodToken(DynamicILInfo ilInfo, MethodBase methodInfo)
        {
            var declaringType = methodInfo.DeclaringType?.ResolveVirtualType() ?? throw new InvalidOperationException($"Method {methodInfo} declaring type is null");
            return declaringType.IsGenericType
                ? ilInfo.GetTokenFor(methodInfo.MethodHandle, declaringType.TypeHandle)
                : ilInfo.GetTokenFor(methodInfo.MethodHandle);
        }

        private void AddFixup(int instructionPosition, BinaryWriter writer, IlInstruction target, bool isSmallOffset = false)
        {
            _fixups.Add(new LabelFixup(instructionPosition, target, writer.BaseStream.Position, isSmallOffset));
            if (isSmallOffset) {
                writer.Write((byte)0);
            } else {
                writer.Write(0);
            }
        }

        void UpdateStackSize(OpCode opcode)
        {
            int flags = (int)opcode.GetFieldValue("m_flags");
            
            _maxMidStackCur += StackChange(flags);
            
            if (_maxMidStackCur > _maxMidStack)
                _maxMidStack = _maxMidStackCur;
            else if (_maxMidStackCur < 0) 
                _maxMidStackCur = 0;

            if (EndsUncondJmpBlk(flags)) {
                _maxStackSize += _maxMidStack;
                _maxMidStack = 0;
                _maxMidStackCur = 0;
            }
        }
        
        bool EndsUncondJmpBlk(int flags)
        {
            return (flags & 0x1000000) != 0;
        }
        
        internal int StackChange(int flags)
        {
            return flags >> 28;
        }
        
        private void SetCode(DynamicILInfo ilInfo) {

            using var ms = new MemoryStream();
            using var buffer = new BinaryWriter(ms);

            foreach (var instruction in _instructionList) {
                var positionBefore = (int)ms.Position;
                
                _instructionOffsets[instruction] = positionBefore;
                
                Emit(ilInfo, instruction, buffer);

                var emitted = new Span<byte>(ms.GetBuffer(), positionBefore, (int) (ms.Position - positionBefore));

                foreach (var b in emitted) {
                    instruction.Debug += b.ToString("X2");
                }

                UpdateStackSize(instruction.OpCode);
            }

            var result = ms.ToArray();
            
            foreach (var fixup in _fixups) {
                var source = fixup.FixupSource;
                var targetInstructionOffset = _instructionOffsets[fixup.Target];
                var offset = (targetInstructionOffset - source);

                if (fixup.IsSmallOffset) {
                    result[fixup.Position] = (byte)offset;
                } else {
                    OverwriteInt32(offset, (int) fixup.Position, result);
                }
            }

            // increase max stack size to accomodate for rewriters
            ilInfo.SetCode(result, _delegateBuilder.MethodInfo.MaxStackSize + 6);
        }

        private void SetLocalSignature(DynamicILInfo ilInfo) {
            var sig = SignatureHelper.GetLocalVarSigHelper();
            
            foreach (var local in _methodBody.Locals) 
                sig.AddArgument(local.LocalType.ResolveVirtualType());
            
            ilInfo.SetLocalSignature(sig.GetSignature());
        }

        private void SetExceptions(DynamicILInfo ilInfo)
        {
            var tryBlocks = _methodBody.TryBlocks;
            if (tryBlocks.Length == 0) 
                return;

            // FAT exception header
            int size = 4 + 24 * tryBlocks.Length;
            byte[] exceptions = new byte[size];

            exceptions[0] = 0x01 | 0x40; //Offset: 0, Kind: CorILMethod_Sect_EHTable | CorILMethod_Sect_FatFormat
            OverwriteInt32(size, 1, exceptions);  // Offset: 1, DataSize: n * 24 + 4

            int pos = 4;
            
            
            foreach (var tryBlock in tryBlocks) {
                var flags = getFlags(tryBlock.HandlerType);
                
                var tryBlockStart = _instructionOffsets[tryBlock.Start];
                var tryBlockLength = _instructionOffsets[tryBlock.End] - tryBlockStart;
                var handlerStart = _instructionOffsets[tryBlock.HandlerStart];
                var handlerEnd = _instructionOffsets[tryBlock.HandlerEnd];
                
                OverwriteInt32((int)flags, pos, exceptions); pos += 4;
                OverwriteInt32(tryBlockStart, pos, exceptions); pos += 4;
                OverwriteInt32(tryBlockLength, pos, exceptions); pos += 4;
                OverwriteInt32(handlerStart, pos, exceptions); pos += 4;
                OverwriteInt32(handlerEnd - handlerStart, pos, exceptions); pos += 4;
                
                switch (flags) {
                    case ExceptionHandlingClauseOptions.Clause:
                        int token = ilInfo.GetTokenFor(tryBlock.CatchType.TypeHandle);
                        OverwriteInt32(token, pos, exceptions);
                        break;
                    case ExceptionHandlingClauseOptions.Filter:
                        var filterStart = _instructionOffsets[tryBlock.FilterStart];
                        OverwriteInt32(filterStart, pos, exceptions);
                        break;
                    case ExceptionHandlingClauseOptions.Fault:
                        throw new NotSupportedException("dynamic method does not support fault clause");
                    case ExceptionHandlingClauseOptions.Finally:
                        break;
                }
                pos += 4;
            }

            ilInfo.SetExceptions(exceptions);

            ExceptionHandlingClauseOptions getFlags(string flags)
            {
                switch (flags) {
                    case "Catch": return ExceptionHandlingClauseOptions.Clause;
                    case "Filter": return ExceptionHandlingClauseOptions.Filter;
                    case "Finally": return ExceptionHandlingClauseOptions.Finally;
                    case "Fault": return ExceptionHandlingClauseOptions.Fault;
                }

                throw new NotImplementedException(flags);
            }
        }
        
        public static void OverwriteInt32(int value, int pos, byte[] array) {
            array[pos++] = (byte)value;
            array[pos++] = (byte)(value >> 8);
            array[pos++] = (byte)(value >> 16);
            array[pos++] = (byte)(value >> 24);
        }
        
        internal class LabelFixup
        {
            public int FixupSource { get; }
            public IlInstruction Target { get; }
            public long Position { get; }
            public bool IsSmallOffset { get; }

            public LabelFixup(int fixupSource, IlInstruction target, long position, bool isSmallOffset)
            {
                FixupSource = fixupSource;
                Target = target;
                Position = position;
                IsSmallOffset = isSmallOffset;
            }
        }
    }
}
#endif