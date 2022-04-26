using System;

namespace LiveSharp.Runtime.IL
{
    class IlCompilationException : Exception
    {
        private readonly Exception _inner;
        private readonly DelegateBuilder _metadata;

        public IlCompilationException(Exception inner, DelegateBuilder metadata)
        {
            _inner = inner;
            _metadata = metadata;
        }

        public override string Message { 
            get {
                return $"{nameof(IlCompilationException)}: {Environment.NewLine}{_metadata.MethodElement}";
            }
        }

        public override string ToString()
        {
            return $"{nameof(IlCompilationException)}: {_inner}";
        }
    }
}