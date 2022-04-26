using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiveSharp.Rewriters
{
    public class IlRewritersProcessor : RewriterBase
    {
        private readonly ModuleDefinition _mainModule;
        private readonly List<IIlRewriter> _rewriters = new();

        public IlRewritersProcessor(ModuleDefinition mainModule)
        {
            _mainModule = mainModule;
        }

        public override void ProcessSupportModule(ModuleDefinition module)
        {
            ProcessModule(module);
        }

        public override void ProcessMainModuleType(TypeDefinition type)
        {
            ProcessType(type);
        }

        public override void Rewrite()
        {
            var allTypes = _mainModule.GetAllTypes();
            var allMethods = allTypes.SelectMany(t => t.Methods).ToArray();

            foreach (var rewriter in _rewriters)
                rewriter.InterceptorMethod = _mainModule.ImportReference(rewriter.InterceptorMethod);
            
            if (_rewriters.Count > 0) {
                foreach (var method in allMethods) {
                    if (!method.HasBody)
                        continue;
            
                    var instructions = method.Body.Instructions;
                    ILProcessor processor = null;
                    var currentInstruction = instructions.FirstOrDefault();

                    while (currentInstruction != null) {
                        foreach (var rewriter in _rewriters) {
                            if (rewriter.Matches(currentInstruction)) {
                                processor ??= method.Body.GetILProcessor();
                                currentInstruction = rewriter.Rewrite(method, currentInstruction, processor);
                            }
                        }

                        currentInstruction = currentInstruction.Next;
                    }
                }
            }
        }

        private void ProcessModule(ModuleDefinition module)
        {
            foreach (var type in module.GetAllTypes())
                ProcessType(type);
        }

        private void ProcessType(TypeDefinition type)
        {
            foreach (var method in type.Methods) {
                var attributes = method.CustomAttributes;
                var interceptCallAttribute = attributes.FirstOrDefault(ca => ca.AttributeType.FullName == "LiveSharp.LiveSharpRewrite/InterceptCallsToAttribute");

                if (interceptCallAttribute != null)
                    _rewriters.Add(new InterceptCallToRewriter(interceptCallAttribute, method));

                var interceptCallsToAnyAttribute = attributes.FirstOrDefault(ca => ca.AttributeType.FullName == "LiveSharp.LiveSharpRewrite/InterceptCallsToAnyAttribute");

                if (interceptCallsToAnyAttribute != null)
                    _rewriters.Add(new InterceptCallToAnyRewriter(interceptCallsToAnyAttribute, method));
            }
        }
    }
}