namespace LiveSharp
{
    public interface ILiveSharpDashboard
    {
        void Configure(ILiveSharpRuntime app);
        void Run(ILiveSharpRuntime app);
    }
}