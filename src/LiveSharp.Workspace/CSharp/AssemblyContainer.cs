using System;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LiveSharp.CSharp
{
    public class AssemblyContainer : IDisposable
    {
        private readonly AssemblyDefinition _assemblyDefinitionRewritten;
        private readonly AssemblyDefinition _assemblyDefinitionOriginal;
        private bool _isDisposed;

        public AssemblyDefinition AssemblyDefinitionOriginal {
            get {
                if (_isDisposed)
                    throw new InvalidOperationException("Cannot access a disposed assembly");
                return _assemblyDefinitionOriginal;
            }
        }
        
        public AssemblyDefinition AssemblyDefinitionForRewrite {
            get {
                if (_isDisposed)
                    throw new InvalidOperationException("Cannot access a disposed assembly");
                return _assemblyDefinitionRewritten;
            }
        }

        public AssemblyContainer(MemoryStream memoryStream, MemoryStream pdbMemoryStream, IAssemblyResolver resolve)
        {
            _assemblyDefinitionOriginal = CreateAssemblyDefinition(memoryStream, pdbMemoryStream, resolve);
            _assemblyDefinitionRewritten = CreateAssemblyDefinition(memoryStream, pdbMemoryStream, resolve);
        }
        
        private AssemblyDefinition CreateAssemblyDefinition(MemoryStream memoryStream, MemoryStream pdbMemoryStream, IAssemblyResolver resolver)
        {
            var codeStream = new MemoryStream(memoryStream.ToArray());
            var symbolStream = new MemoryStream(pdbMemoryStream.ToArray());
            
            return AssemblyDefinition.ReadAssembly(codeStream, new ReaderParameters {
                ReadSymbols = true,
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                SymbolStream = symbolStream,
                AssemblyResolver = resolver
            });
        }
        
        public void Dispose()
        {
            _assemblyDefinitionOriginal.Dispose();
            _assemblyDefinitionRewritten.Dispose();
            _isDisposed = true;
        }
    }
}