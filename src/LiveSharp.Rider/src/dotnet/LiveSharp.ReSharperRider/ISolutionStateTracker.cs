using JetBrains.Annotations;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;

namespace LiveSharp.ReSharperRider
{
    public interface ISolutionStateTracker
    {
        [CanBeNull]
        ISolution Solution { get; }
        ISignal<ISolution> AfterSolutionOpened { get; }
        ISignal<ISolution> BeforeSolutionClosed { get; }
    }
}