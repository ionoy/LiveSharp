using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace LiveSharp.Rewriters.Serialization
{
    public class TypeElement
    {
        public int Token { get; private set; } = -1;
        public IReadOnlyList<int> ArrayRanks { get; }
        public bool TypeIsByReference { get; }
        public string AssemblyFullName { get; }
        public string TypeFullName { get; }
        public int[] GenericArguments { get; }
        public bool IsGenericParameter { get; }
        public int? GenericParameterPosition { get; }
        public bool? GenericParameterOwnerIsMethod { get; }

        public bool IsAsyncStateMachine { get; set; }

        public TypeElement(IReadOnlyList<int> arrayRanks, bool isByRef, string assemblyFullName, string typeFullName, int[] genericArguments = null, bool isGenericParameter = false, bool isAsyncStateMachine = false,
            int? genericParameterPosition = null, bool? genericParameterOwnerIsMethod = null)
        {
            ArrayRanks = arrayRanks;
            TypeIsByReference = isByRef;
            AssemblyFullName = assemblyFullName;
            TypeFullName = typeFullName;
            GenericArguments = genericArguments ?? new int[0];
            IsGenericParameter = isGenericParameter;
            IsAsyncStateMachine = isAsyncStateMachine;
            GenericParameterPosition = genericParameterPosition;
            GenericParameterOwnerIsMethod = genericParameterOwnerIsMethod;
        }

        public void SetToken(int token)
        {
            Token = token;
        }

        public XElement ToElement()
        {
            if (Token == -1)
                throw new InvalidOperationException("Type token is not initialized");
            
            var genericArgumentsString = GenericArguments != null 
                ? string.Join(",", GenericArguments)
                : "";

            var attributes = new List<XAttribute> {
                new(nameof(Token), Token),
                new(nameof(ArrayRanks), string.Join(",", ArrayRanks)),
                new(nameof(TypeIsByReference), TypeIsByReference),
                new(nameof(AssemblyFullName), AssemblyFullName),
                new(nameof(TypeFullName), TypeFullName),
                new(nameof(GenericArguments), genericArgumentsString),
                new(nameof(IsGenericParameter), IsGenericParameter),
                new(nameof(IsAsyncStateMachine), IsAsyncStateMachine)
            };

            if (GenericParameterPosition != null)
                attributes.Add(new XAttribute(nameof(GenericParameterPosition), GenericParameterPosition));
            
            if (GenericParameterOwnerIsMethod != null)
                attributes.Add(new XAttribute(nameof(GenericParameterOwnerIsMethod), GenericParameterOwnerIsMethod));

            var element = new XElement("Type",
                attributes
            );

            return element;
        }

        public override string ToString()
        {
            return $"{TypeFullName} ({Token})";
        }

        protected bool Equals(TypeElement other)
        {
            return ArrayRanksEqual(ArrayRanks, other.ArrayRanks) && 
                   TypeIsByReference == other.TypeIsByReference && 
                   AssemblyFullName == other.AssemblyFullName && 
                   TypeFullName == other.TypeFullName && 
                   GenericArguments == other.GenericArguments && 
                   IsGenericParameter == other.IsGenericParameter;
        }

        private bool ArrayRanksEqual(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
                if (left[i] != right[i])
                    return false;
            
            return false;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TypeElement) obj);
        }

        public override int GetHashCode()
        {
            var a = ArrayRanks?.GetHashCode() ?? 0;
            var b = TypeIsByReference.GetHashCode();
            var c = AssemblyFullName?.GetHashCode() ?? 0;
            var d = TypeFullName?.GetHashCode() ?? 0;
            var e = GenericArguments?.GetHashCode() ?? 0;
            var f = IsGenericParameter.GetHashCode();
            
            return a + b + c + d + e + f;
        }
    }
}