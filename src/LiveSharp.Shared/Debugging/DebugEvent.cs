using LiveSharp.Shared.Infrastructure;
using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Debugging
#else
namespace LiveSharp.Shared.Debugging
#endif
{
    public abstract class DebugEvent
    {
        public int InvocationId { get; set; }
        public long Timestamp { get; set; }

        protected DebugEvent()
        {
            Timestamp = DateTime.Now.ToBinary();
        }
    }
}