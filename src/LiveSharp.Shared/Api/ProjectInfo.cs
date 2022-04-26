#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Api
#else
namespace LiveSharp.Shared.Api
#endif
{
    public class ProjectInfo : XmlMessage<ProjectInfo>
    {
        public string SolutionPath { get; set; }
        public string ProjectName { get; set; }
        public string ProjectDir { get; set; }
        public string ProjectReferences { get; set; }
        public string NuGetPackagePath { get; set; }
        public bool IsLiveBlazor { get; set; }

        public string GetProjectId()
        {
            return SolutionPath + '\n' + ProjectName +  '\n' + NuGetPackagePath;
        }
    }
}