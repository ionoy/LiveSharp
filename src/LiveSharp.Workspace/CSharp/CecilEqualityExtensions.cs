using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LiveSharp.CSharp
{
    public static class CecilEqualityExtensions
    {
        private static bool SameAs(this TypeReference left, TypeReference right)
        {
            if (left is TypeSpecification ls && right is TypeSpecification rs)
                return SameAs(ls.ElementType, rs.ElementType);

            if (left is GenericParameter lgp && right is GenericParameter rgp)
                return lgp.Position == rgp.Position;
            
            return left.Scope.Name == right.Scope.Name && left.FullName == right.FullName;
        }

        public static bool SameAs(this MethodReference left, MethodReference right)
        {
            if (left.Name != right.Name)
                return false;
            
            if (!left.DeclaringType.SameAs(right.DeclaringType))
                return false;

            return SameSignatureAs(left, right);
        }

        public static bool SameSignatureAs(this MethodReference originalMethod, MethodReference updatedMethod)
        {
            if (originalMethod.Name != updatedMethod.Name)
                return false;

            if (originalMethod.Parameters.Count != updatedMethod.Parameters.Count)
                return false;

            for (int i = 0; i < originalMethod.Parameters.Count; i++) {
                var orgParamType = originalMethod.Parameters[i].ParameterType;
                var updatedParamType = updatedMethod.Parameters[i].ParameterType;

                if (!orgParamType.SameAs(updatedParamType))
                    return false;
            }

            // No need to check for generic parameters because C# doesn't allow overloading by them
            
            return true;
        }
        
        public static bool SameAs(this MethodDefinition originalMethod, MethodDefinition updatedMethod)
        {
            if (!originalMethod.SameSignatureAs(updatedMethod)) 
                return false;

            if (!originalMethod.ReturnType.SameAs(updatedMethod.ReturnType))
                return false;

            var oldMethodBody = originalMethod.Body;
            var newMethodBody = updatedMethod.Body;

            if (oldMethodBody == null && newMethodBody != null)
                return false;
            
            if (newMethodBody == null && oldMethodBody != null)
                return false;

            // basically a null check
            if (oldMethodBody == newMethodBody)
                return true;

            if (oldMethodBody.Instructions.Count != newMethodBody.Instructions.Count)
                return false;

            for (int i = 0; i < oldMethodBody.Instructions.Count; i++) {
                var oldInstruction = oldMethodBody.Instructions[i];
                var newInstruction = newMethodBody.Instructions[i];

                if (!InstructionsEqual(oldInstruction, newInstruction))
                    return false;
            }            
            
            return true;
        }

        private static bool InstructionsEqual(Instruction left, Instruction right)
        {
            if (left.OpCode != right.OpCode)
                return false;

            if (left.Operand is TypeReference leftType && right.Operand is TypeReference rightType)
                return leftType.SameAs(rightType);
            
            if (left.Operand is MethodReference lm && right.Operand is MethodReference rm)
                return lm.SameAs(rm);
            
            if (left.Operand is MemberReference lmr && right.Operand is MemberReference rmr) {
                if (!lmr.DeclaringType.SameAs(rmr.DeclaringType))
                    return false;
                
                // just check the name, because c# doesn't allow two members with the same name besides method overloading
                return lmr.Name == rmr.Name;
            }
            
            return left.ToString() == right.ToString();
        }
        
    }
}