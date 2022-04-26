using LiveSharp.Runtime.IL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiveSharp.Runtime.Virtual
{
    public class GenericTypeInstance : TypeDelegator
    {
        public List<Type> GenericArguments { get; } = new();

        public GenericTypeInstance(Type delegatingType, IEnumerable<Type> genericArguments) : base(delegatingType)
        {
            GenericArguments.AddRange(genericArguments);
        }

        public override Type MakeArrayType() => new GenericTypeInstance(UnderlyingSystemType.MakeArrayType(), GenericArguments);

        public override Type MakeArrayType(int rank) => new GenericTypeInstance(UnderlyingSystemType.MakeArrayType(rank), GenericArguments);

        public override Type MakeByRefType() => new GenericTypeInstance(UnderlyingSystemType.MakeByRefType(), GenericArguments);

        public override bool ContainsGenericParameters => GenericArguments.Any(a => a is GenericTypeParameter || a.IsGenericParameter);

        public override Type[] GetGenericArguments() => GenericArguments.ToArray();

        public override Type GetGenericTypeDefinition() => UnderlyingSystemType.GetGenericTypeDefinition();
    }

}