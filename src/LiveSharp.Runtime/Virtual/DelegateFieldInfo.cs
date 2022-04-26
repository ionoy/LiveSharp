using System;

namespace LiveSharp.Runtime.Virtual
{
    class DelegateFieldInfo
    {
        public Type MethodDeclaringType { get; }
        public string MethodName { get; }
        public Type MethodReturnType { get; }
        public string MethodIdentifier { get; }
            
        public Type FieldDeclaringType { get; }
        public string FieldName { get; }

        public DelegateFieldInfo(Type methodDeclaringType, string methodName, Type methodReturnType, string methodIdentifier, Type fieldDeclaringType, string fieldName)
        {
            MethodDeclaringType = methodDeclaringType;
            MethodName = methodName;
            MethodReturnType = methodReturnType;
            MethodIdentifier = methodIdentifier;
            FieldDeclaringType = fieldDeclaringType;
            FieldName = fieldName;
        }
    }
}