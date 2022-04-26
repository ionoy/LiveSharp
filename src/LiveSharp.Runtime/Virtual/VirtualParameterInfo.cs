using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LiveSharp.Runtime
{
    class VirtualParameterInfo : ParameterInfo
    {
        private Type _parameterType;
        private string _name;
        
        public VirtualParameterInfo(Type parameterType, string name)
        {
            _parameterType = parameterType;
            _name = name;
        }

        public override Type ParameterType => _parameterType;
        public override string Name => _name;
    }
}