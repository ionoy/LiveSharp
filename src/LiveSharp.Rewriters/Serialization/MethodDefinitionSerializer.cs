using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace LiveSharp.Rewriters.Serialization
{
    [DebuggerDisplay("{IdentifierWithoutTypeName}")]
    public class MethodDefinitionSerializer
    {
        public string MemberName { get; }
        public string IdentifierWithoutTypeName { get; }
        public string MethodBodyIl { get; set; }
        public int DebugLevel { get; }
        private Dictionary<int, XElement> Members { get; } = new();
        public MethodDefinition MethodDefinition { get; }

        public bool NeedsDebugging { get; private set; }

        private readonly Dictionary<string, int> _stringMap = new();
        private readonly DocumentSerializer _documentSerializer;

        public MethodDefinitionSerializer(MethodDefinition methodMethodDefinition, DocumentSerializer documentSerializer)
        {
            _documentSerializer = documentSerializer;

            MethodDefinition = methodMethodDefinition;
            MemberName = methodMethodDefinition.Name;
            IdentifierWithoutTypeName = methodMethodDefinition.GetMethodIdentifier(false);
            DebugLevel = 0;
        }

        public XElement Serialize()
        {
            var methodBody = MethodDefinition.Body;
            var containingMethod = MethodDefinition;
            var containingType = MethodDefinition.DeclaringType;
            var parameters = MethodDefinition.Parameters.Select(p => new XElement("Parameter", new XAttribute("Name", p.Name), new XAttribute("Type", _documentSerializer.GetTypeToken(p.ParameterType, containingMethod, containingType)))).ToArray();
            var locals = MethodDefinition.Body.Variables.Select((p, index) => SerializeLocalVariable(index, p, containingMethod, containingType)).ToArray();
            
            MethodBodyIl = string.Join("\n", methodBody.Instructions.Select(i => SerializeInstruction(i, methodBody, containingMethod, containingType)));
            
            var returnType = _documentSerializer.GetTypeToken(MethodDefinition.ReturnType, containingMethod, containingType);
            var exceptionHandlers = SerializeExceptionHandler(MethodDefinition.Body);
            var declaringTypeName = MethodDefinition.DeclaringType.FullName.Replace("/", "+");
            var strings = _stringMap.Select(s => new XElement("S", new XAttribute("Id", s.Value), new XText(s.Key)));
            var genericParameters = string.Join(",",MethodDefinition.CollectGenericParameters().Select(gp => _documentSerializer.GetTypeToken(gp, containingMethod, containingType)));
            
            
            return new XElement("Method", 
                new XAttribute("Name", MethodDefinition.Name),
                new XAttribute("MethodIdentifier", declaringTypeName + " " + IdentifierWithoutTypeName), 
                new XAttribute("DeclaringType", _documentSerializer.GetTypeToken(containingType, null, null)),
                new XAttribute("DebugLevel", DebugLevel),
                new XAttribute("IsStatic", MethodDefinition.IsStatic),
                new XAttribute("ReturnType", returnType),
                new XAttribute("MaxStackSize", MethodDefinition.Body.MaxStackSize),
                new XAttribute("IsGeneric", MethodDefinition.GenericParameters.Count > 0),
                new XElement("Parameters", parameters),
                new XElement("GenericParameters", genericParameters),
                new XElement("Members", Members.Values),
                new XElement("Locals", locals),
                new XElement("ExceptionHandlers", exceptionHandlers),
                new XElement("Strings", strings),
                new XElement("IL", MethodBodyIl));
        }

        private XElement SerializeGenericParameter(GenericParameter genericParameter)
        {
            if (genericParameter == null) throw new ArgumentNullException(nameof(genericParameter));
            
            return new("GenericParameter", new XAttribute("Name", genericParameter.Name));
        }

        private XElement SerializeLocalVariable(int index, VariableDefinition p, MethodDefinition containingMethod, TypeDefinition containingType)
        {
            if (containingMethod.DebugInformation.TryGetName(p, out var name))
                return new XElement("Local", new XAttribute("Name", name), new XAttribute("Type", _documentSerializer.GetTypeToken(p.VariableType, containingMethod, containingType)));
            
            return new XElement("Local", new XAttribute("Name", "$loc_" + index), new XAttribute("Type", _documentSerializer.GetTypeToken(p.VariableType, containingMethod, containingType)));
        }

        private IEnumerable<XElement> SerializeExceptionHandler(MethodBody methodBody)
        {
            var result = new List<XElement>();
            var instructionIndices = new Dictionary<int, int>();

            for (var index = 0; index < methodBody.Instructions.Count; index++) {
                var instruction = methodBody.Instructions[index];
                instructionIndices[instruction.Offset] = index;
            }
            
            foreach (var exceptionHandler in methodBody.ExceptionHandlers) {
                var tryStart = instructionIndices[exceptionHandler.TryStart.Offset];
                var tryEnd = instructionIndices[exceptionHandler.TryEnd.Offset];
                
                var tryElement = new XElement("Try", new XAttribute("Start", tryStart), new XAttribute("End", tryEnd));
                tryElement.Add(serializeTryHandler(exceptionHandler));
                result.Add(tryElement);
            }

            XElement serializeTryHandler(ExceptionHandler handler)
            {
                var catchType = handler.CatchType != null ? _documentSerializer.GetTypeToken(handler.CatchType, methodBody.Method, methodBody.Method.DeclaringType) : -1;
                var filterStart = handler.FilterStart != null ? instructionIndices[handler.FilterStart.Offset] : -1;
                
                return new XElement("Handler", 
                    new XAttribute("Type", handler.HandlerType), 
                    new XAttribute("CatchType", catchType),
                    new XAttribute("Start", instructionIndices[handler.HandlerStart.Offset]),
                    new XAttribute("End", instructionIndices[handler.HandlerEnd.Offset]),
                    new XAttribute("FilterStart", filterStart));
            }
            
            return result;
        }

        string SerializeMember(MemberReference member)
        {
            var token = member.MetadataToken.ToInt32();
                
            if (Members.TryGetValue(token, out _))
                return token.ToString();
                
            var memberName = member.Name;
            var memberType = getMemberType();
            
            if (member is MethodReference method) {
                if (method is GenericInstanceMethod genericInstanceMethod) {
                    var parameterTypes = method.Parameters.Select(p => _documentSerializer.GetTypeToken(p.ParameterType, method, method.DeclaringType)).ToArray();
                    var genericArguments = genericInstanceMethod.GenericArguments;
                    var genericParameterTypes = genericArguments.Select(arg => _documentSerializer.GetTypeToken(arg, method, method.DeclaringType));
                    var returnType = method.ReturnType;
                    
                    // if (returnType is GenericInstanceType git) {
                    //     var elementType = git.ElementType;
                    //     var newGit = new GenericInstanceType(elementType);
                    //     for (int i = 0; i < elementType.GenericParameters.Count; i++) {
                    //         var gp = elementType.GenericParameters[i];
                    //         var resolvedType = DocumentSerializer.FindGenericParameterUpstream(gp, ) 
                    //     }
                    //     
                    // }
                    
                    Members[token] = new XElement("Member",
                                        new XAttribute("Token", token),
                                        new XAttribute("ContainingType", _documentSerializer.GetTypeToken(member.DeclaringType, method, method.DeclaringType)),
                                        new XAttribute("Name", memberName),
                                        new XAttribute("MemberType", memberType),
                                        new XAttribute("ReturnType", _documentSerializer.GetTypeToken(method.ReturnType, method, method.DeclaringType)),
                                        new XAttribute("ParameterTypes", string.Join(",", parameterTypes)),
                                        new XAttribute("GenericArguments", string.Join(",", genericParameterTypes)));
                } else {
                    var parms = method.Parameters.Select(p => _documentSerializer.GetTypeToken(p.ParameterType, method, method.DeclaringType)).ToArray();
                    var parameterTypes = string.Join(",", parms);
                    
                    Members[token] = new XElement("Member",
                        new XAttribute("Token", token),
                        new XAttribute("ContainingType", _documentSerializer.GetTypeToken(member.DeclaringType, method, method.DeclaringType)),
                        new XAttribute("Name", memberName),
                        new XAttribute("ReturnType", _documentSerializer.GetTypeToken(method.ReturnType, method, method.DeclaringType)),
                        new XAttribute("MemberType", memberType),
                        new XAttribute("ParameterTypes", parameterTypes));
                }
                
            } else {
                var fieldOrPropertyType = -1;
                if (member is FieldReference fld)
                    fieldOrPropertyType = _documentSerializer.GetTypeToken(fld.FieldType, null, fld.DeclaringType);
                if (member is PropertyReference property)
                    fieldOrPropertyType = _documentSerializer.GetTypeToken(property.PropertyType, null, property.DeclaringType);
                
                Members[token] = new XElement("Member",
                    new XAttribute("Token", token),
                    new XAttribute("MemberType", memberType),
                    new XAttribute("ContainingType", _documentSerializer.GetTypeToken(member.DeclaringType, null, null)),
                    new XAttribute("Type", fieldOrPropertyType),
                    new XAttribute("Name", memberName));
            }

            string getMemberType()
            {
                if (member is MethodReference) return "Method";
                if (member is PropertyReference) return "Property";
                if (member is FieldReference) return "Field";
                if (member is EventReference) return "Event";
                
                throw new InvalidOperationException("Unknown member reference: " + member);
            }
            
            return token.ToString();
        }

        string SerializeInstruction(Instruction i, MethodBody instructions, MethodReference containingMethod, TypeReference containingType)
        {
            if (i.OpCode == OpCodes.Call && i.Operand is MethodReference mr && mr.Name == "Debug" && mr.DeclaringType.FullName == "LiveSharp.App.LiveSharp") {
                NeedsDebugging = true;
            }
            
            if (i.Operand is TypeReference type)
                return i.OpCode.Name + " t" + _documentSerializer.GetTypeToken(type, containingMethod, containingType) + " //" + i.Operand;
                
            if (i.Operand is MemberReference member)
                return i.OpCode.Name + " t" + SerializeMember(member) + " //" + i.Operand;

            if (i.Operand is string str)
                return i.OpCode.Name + " s" + SerializeString(str);

            if (i.Operand is Instruction instruction)
                return i.OpCode.Name + " i" + instructions.Instructions.IndexOf(instruction);

            if (i.Operand is Instruction[] instructionArray) {
                var instructionString = string.Join(",", instructionArray.Select(instr => instructions.Instructions.IndexOf(instr)));
                return i.OpCode.Name + " ia" + instructionString;
            }

            if (i.Operand is VariableDefinition variable)
                return i.OpCode.Name + " i" + instructions.Variables.IndexOf(variable);
            
            if (i.Operand is ParameterDefinition parameter)
                return i.OpCode.Name + " i" + GetArgumentIndex(containingMethod, parameter);

            if (i.OpCode.OperandType == OperandType.InlineI)
                return i.OpCode.Name + " i" + (int)i.Operand;
            
            if (i.OpCode.OperandType == OperandType.ShortInlineI)
                return i.OpCode.Name + " i" + (SByte)i.Operand;
            
            if (i.OpCode.OperandType == OperandType.InlineR)
                return i.OpCode.Name + " d" + (double)i.Operand;
            
            if (i.OpCode.OperandType == OperandType.ShortInlineR)
                return i.OpCode.Name + " f" + (float)i.Operand;
            
            if (i.Operand != null)
                return i.OpCode.Name + " " + i.Operand;
            

            return i.OpCode.Name;
        }

        private static int GetArgumentIndex(MethodReference containingMethod, ParameterDefinition parameter)
        {
            var parameterIndex = containingMethod.Parameters.IndexOf(parameter);
            
            if (containingMethod.Resolve().IsStatic)
                return parameterIndex;
            
            // Non-static methods reserve 0 slot for `this`
            return parameterIndex + 1;
        }

        private int SerializeString(string str)
        {
            if (_stringMap.TryGetValue(str, out var key))
                return key;

            return _stringMap[str] = _stringMap.Count;
        }
    }
}