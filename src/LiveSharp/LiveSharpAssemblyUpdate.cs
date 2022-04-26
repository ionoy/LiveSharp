using System;

namespace LiveSharp
{
    [Serializable]
    public class LiveSharpAssemblyUpdate
    {
        public string Name { get; set; }
        public byte[] AssemblyBuffer { get; set; }
        public byte[] SymbolsBuffer { get; set; }

        public LiveSharpAssemblyUpdate()
        { }

        public LiveSharpAssemblyUpdate(string name, byte[] assemblyBuffer, byte[] symbolsBuffer)
        {
            Name = name;
            AssemblyBuffer = assemblyBuffer;
            SymbolsBuffer = symbolsBuffer;
        }
    }
}