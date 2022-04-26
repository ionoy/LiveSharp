using JetBrains.ProjectModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.ReSharperRider
{
    [SolutionComponent]
    public class SettingsModel  
    {
        public bool DoNotShowAtStartup { get; set; }
        public bool DebugAlways { get; set; }       
 
    }

}
