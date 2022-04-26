using System;
using System.Collections.Generic;
using System.Text;

namespace LiveSharp.Common.Events
{
    public class WorkspaceEvent : Event
    {
        public string Description { get; set; }
        public WorkspaceEvent()
        {
            Description = "";
        }
        
        public WorkspaceEvent(string description)
        {
            Description = description;
        }
    }
}
