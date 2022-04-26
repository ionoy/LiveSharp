using System;

namespace LiveSharp
{
    public interface ILiveSharpUpdateHandler : IDisposable
    {
        void Attach(ILiveSharpRuntime runtime);
    }
}