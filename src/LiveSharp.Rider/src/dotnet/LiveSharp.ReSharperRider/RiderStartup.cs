using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.ProjectModel;
using JetBrains.Rider.Model;
using JetBrains.DataFlow;
using JetBrains.Util;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Host.Features;
using JetBrains.Application.Settings.Implementation;

namespace LiveSharp.ReSharperRider
{
    [SolutionComponent]
    public class RiderStartup
    {
        private LiveSharpModel model; 
        
        public RiderStartup(Lifetime lifetime, ISolutionStateTracker solutionStateTracker, ISolution solution)
        {

            model = solution.GetProtocolSolution().GetLiveSharpModel();


            PerformModelAction(mdl => mdl.FileEvent.Advise(lifetime, a => { OnFileEvent(a); }));
                                    

            //Settings 
            //var settingsStore = context.GetComponent<SettingsStore>();

            //Check Status 

            ////////////// 
            
           

            solutionStateTracker.AfterSolutionOpened.Advise(lifetime,
                () => {

                    MessageBox.ShowInfo("Finished loading the solution");

                });
                      

        }

        public void PerformModelAction(Action<LiveSharpModel> action)
        {
            action(model);
        }

        public void OnFileEvent(FileEvent file)
        {
            MessageBox.ShowInfo($"Saved {file.FileName} - Protocol comm ok");

        }

        public bool HasMethod(object objectToCheck, string methodName)
        {
            var type = objectToCheck.GetType();
            return type.GetMethod(methodName) != null;
        }
    }
}
