using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSharp.ServerClient
{
    public static class NumericTypes
    {
        private static readonly HashSet<Type> Types = new HashSet<Type> {
            typeof(int),
            typeof(double),
            typeof(decimal),
            typeof(long),
            typeof(short),
            typeof(sbyte),
            typeof(byte),
            typeof(ulong),
            typeof(ushort),
            typeof(uint),
            typeof(float)
        };
        
        public static bool TypeIsNumeric(Type type)
        {
            return Types.Contains(Nullable.GetUnderlyingType(type) ?? type);
        }

        public static bool TypeIsNumeric(string fullname)
        {
            return Types.Any(t => t.FullName == fullname);
        }
    }
}
