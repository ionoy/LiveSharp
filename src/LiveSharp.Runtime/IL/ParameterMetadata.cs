using System;

namespace LiveSharp.Runtime.IL
{
    public class ParameterMetadata
    {
        public string ParameterName { get; }
        public Type ParameterType { get; }

        public ParameterMetadata(string parameterName, Type parameterType)
        {
            ParameterName = parameterName;
            ParameterType = parameterType;
        }
    }
}