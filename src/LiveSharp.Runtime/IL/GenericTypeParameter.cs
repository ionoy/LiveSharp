using System;
using System.Diagnostics;
using System.Reflection;

namespace LiveSharp.Runtime.IL
{
    [DebuggerDisplay("{Name}")]
    class GenericTypeParameter : TypeDelegator
    {
        public int Token { get; }
        public override string Name { get; }
        public override bool IsGenericParameter => true;
        public override int GenericParameterPosition { get; }
        public bool IsDeclaredOnMethod { get; }

        public GenericTypeParameter(int token, Type type, string name, int genericParameterPosition, bool isDeclaredOnMethod)
        {
            Token = token;
            Name = name;
            GenericParameterPosition = genericParameterPosition;
            IsDeclaredOnMethod = isDeclaredOnMethod;
            
            this.typeImpl = type;
        }
    }
}