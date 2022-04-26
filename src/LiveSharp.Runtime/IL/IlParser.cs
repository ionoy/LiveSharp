using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace LiveSharp.Runtime.IL
{
    public static class IlParser
    {
        private static readonly Lazy<Dictionary<string, OpCode>> LazyOpCodeMap = new(GetOpCodeMap);
        private static Dictionary<string, OpCode> OpCodeMap => LazyOpCodeMap.Value;
        public static IlInstructionList Parse(Dictionary<string, object> members, List<LocalMetadata> locals, IDictionary<string,string> strings, string il, Dictionary<int, Type> types)
        {
            var instructions = new IlInstructionList(locals);
            var instructionLines = il.Split('\n');
            
            foreach (var instructionLine in instructionLines.Where(i => !string.IsNullOrWhiteSpace(i))) {
                var split = instructionLine.Split(new [] {' '}, 2);
                
                if (split.Length == 0)
                    throw new InvalidOperationException($"Invalid instruction '{instructionLine}'");

                var instructionName = split[0];
                if (instructionName == "stelem.any")
                    instructionName = "stelem";
                
                if (instructionName == "ldelem.any")
                    instructionName = "ldelem";
                
                if (OpCodeMap.TryGetValue(instructionName, out var opCode)) {
                    if (split.Length > 1) 
                        instructions.Append(opCode, ParseOperand(split[1], members, strings, types));
                    else
                        instructions.Append(opCode, null);
                } else {
                    throw new InvalidOperationException($"Invalid instruction '{instructionLine}'");
                }
            }

            var instructionArray = instructions.ToArray();
            var localOpCodes = new[] {
                OpCodes.Ldloc, OpCodes.Ldloca, OpCodes.Ldloc_S, OpCodes.Ldloca_S, OpCodes.Stloc, OpCodes.Stloc_S
            };
            
            foreach (var instruction in instructions) {
                var flowControl = instruction.OpCode.FlowControl;
                
                if (flowControl == FlowControl.Branch || flowControl == FlowControl.Cond_Branch) {
                    var operand = instruction.Operand;
                    if (operand is int instructionIndex) {
                        instruction.Operand = instructionArray[instructionIndex];
                        instructions.AddJumpTarget(instruction, (IlInstruction)instruction.Operand);
                    } else if (operand is int[] instructionIndices) {
                        instruction.Operand = instructionIndices.Select(i => instructionArray[i]).ToArray();
                        foreach (var target in (IlInstruction[])instruction.Operand)
                            instructions.AddJumpTarget(instruction, target);
                    }
                }

                if (localOpCodes.Any(o => o.Value == instruction.OpCode.Value)) {
                    if (instruction.Operand is int localIndex) {
                        instruction.Operand = instructions.Locals[localIndex];
                    } else {
                        throw new InvalidOperationException($"Local instruction has invalid operand {instruction.Operand}");
                    }
                }
            }
            
            return instructions;
        }

        private static object ParseOperand(string operandText, Dictionary<string, object> members, IDictionary<string, string> strings, Dictionary<int, Type> types)
        {
            // Deserialize token
            if (operandText.StartsWith("t")) {
                var typeTokenString = operandText.Substring(1);
                
                // remove comment
                typeTokenString = typeTokenString.Substring(0, typeTokenString.IndexOf(" //", StringComparison.Ordinal));
                
                var typeToken = int.Parse(typeTokenString);
                
                if (types.TryGetValue(typeToken, out var type))
                    return type;
                if (members.TryGetValue(typeTokenString, out var memberInfo))
                    return memberInfo;

                throw new Exception("Invalid metadata token " + typeTokenString);
            }

            if (operandText.StartsWith("s")) {
                var stringToken = operandText.Substring(1);
                
                if (strings.TryGetValue(stringToken, out var str))
                    return str;
                
                throw new Exception("Invalid string token " + stringToken);
            }

            if (operandText.StartsWith("ia")) {
                var intList = operandText.Substring(2).Split(',');

                return intList.Select(intString => int.Parse(intString, NumberStyles.Any, CultureInfo.InvariantCulture)).ToArray();
            }

            if (operandText.StartsWith("i")) {
                var intString = operandText.Substring(1);
                
                if (int.TryParse(intString, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
                    return i;
                
                throw new Exception("Invalid int token " + intString);
            }

            if (operandText.StartsWith("f")) {
                var intString = operandText.Substring(1);
                
                if (float.TryParse(intString, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
                    return i;
                
                throw new Exception("Invalid float token " + intString);
            }

            if (operandText.StartsWith("d")) {
                var intString = operandText.Substring(1);
                
                if (double.TryParse(intString, NumberStyles.Any, CultureInfo.InvariantCulture, out var i))
                    return i;
                
                throw new Exception("Invalid double token " + intString);
            }

            return operandText;
        }

        private static Dictionary<string, OpCode> GetOpCodeMap()
        {
            var result = new Dictionary<string, OpCode>();
            var opCodeFields = typeof(OpCodes).GetFields().Where(f => f.FieldType == typeof(OpCode));
            
            foreach (var opCodeField in opCodeFields) {
                var opCode = (OpCode)opCodeField.GetValue(null);
                result[opCode.Name] = opCode;
            }

            return result;
        }
    }
}