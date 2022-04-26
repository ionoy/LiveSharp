using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LiveSharp.Runtime.IL
{
    public class TryBlockInfo
    {
        public IlInstruction Start { get; set; }
        public IlInstruction End { get; set; }

        public string HandlerType { get; }
        public IlInstruction HandlerStart { get; set; }
        public IlInstruction HandlerEnd { get; set; }

        public IlInstruction FilterStart { get; set; }
        public Type CatchType { get; }

        public ParameterExpression ExceptionParameter { get; set; }
        
        public TryBlockInfo(IlInstruction start, IlInstruction end, string handlerType, IlInstruction handlerStart, IlInstruction handlerEnd, Type catchType, IlInstruction filterStart)
        {
            Start = start;
            End = end;
            HandlerType = handlerType;
            HandlerStart = handlerStart;
            HandlerEnd = handlerEnd;
            CatchType = catchType;
            FilterStart = filterStart;
        }

        public TryBlockInfo Clone()
        {
            return new(Start, End, HandlerType, HandlerStart, HandlerEnd, CatchType, FilterStart);
        }

        public TryBlock ToTryBlock()
        {
            return new(
                Start?.Index ?? -1, 
                End?.Index ?? -1, 
                HandlerType, 
                HandlerStart?.Index ?? -1, 
                HandlerEnd?.Index ?? -1,
                FilterStart?.Index ?? -1,
                CatchType);
        }

        public override string ToString()
        {
            var catchTypeName = CatchType != null ? CatchType.Name : string.Empty;
            return $"try ({Start}:{End}) {HandlerType} {catchTypeName} ({HandlerStart}:{HandlerEnd}) filter ({FilterStart})";
        }
    }

    public class TryBlock
    {
        public int Start { get; set; }
        public int End { get; set; }

        public string HandlerType { get; set; }
        public int HandlerStart { get; set; }
        public int HandlerEnd { get; set; }

        public int FilterStart { get; set; }
        public Type CatchType { get; set; }

        public TryBlock(int start, int end, string handlerType, int handlerStart, int handlerEnd, int filterStart, Type catchType)
        {
            Start = start;
            End = end;
            HandlerType = handlerType;
            HandlerStart = handlerStart;
            HandlerEnd = handlerEnd;
            FilterStart = filterStart;
            CatchType = catchType;
        }

        public TryBlockInfo ToTryBlockInfo(IlInstruction[] instructionsArray)
        {
            return new(
                Start > 0 ? instructionsArray[Start] : null, 
                End > 0 ? instructionsArray[End] : null, 
                HandlerType, 
                HandlerStart > 0 ? instructionsArray[HandlerStart] : null, 
                HandlerEnd > 0 ? instructionsArray[HandlerEnd] : null, 
                CatchType, 
                FilterStart > 0 ? instructionsArray[FilterStart] : null);
        }
    }
}