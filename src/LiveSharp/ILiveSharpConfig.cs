using System.Xml;

namespace LiveSharp
{
    public interface ILiveSharpConfig
    {
        bool TryGetValue(string key, out string value);
        void SetValue(string key, string value);
    }
}