namespace LiveSharp
{
    public interface IUpdatedResource
    {
        string Path { get; }
        string Content { get; }
    }
}