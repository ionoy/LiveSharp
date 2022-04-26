using LiveSharp.Runtime.IL;
using LiveSharp.Runtime.Infrastructure;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace LiveSharp.Runtime.Virtual
{
    public class VirtualMethodBody
    {
        private readonly DocumentMetadata _documentMetadata;
        private readonly IlInstructionList _instructions;
        private readonly ILogger _logger;
        public List<LocalMetadata> Locals => _instructions.Locals;
        public TryBlockInfo[] TryBlocks => _instructions.TryBlocks;
        public IlInstructionList Instructions => _instructions;

        public VirtualMethodBody(DocumentMetadata documentMetadata, IlInstructionList instructions, ILogger logger, bool needInstructionsDevirtualize = true)
        {
            _documentMetadata = documentMetadata;
            _instructions = instructions;
            _logger = logger;
            
            if (needInstructionsDevirtualize)
                _instructions = Devirtualizer.Devirtualize(this, instructions, LiveSharpRuntime.RuntimeExtensions);
        }

        public VirtualMethodBody Clone()
        {
            var newInstructions = _instructions.Clone();
            
            return new VirtualMethodBody(_documentMetadata, newInstructions, _logger, needInstructionsDevirtualize: false);
        }
    }
}