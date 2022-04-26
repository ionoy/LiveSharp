using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LiveSharp.Rewriters
{
    internal interface IIlRewriter
    {
        MethodReference InterceptorMethod { get; set; }
        
        bool Matches(Instruction instruction);
        Instruction Rewrite(MethodDefinition parentMethod, Instruction instruction, ILProcessor ilProcessor);
    }
}