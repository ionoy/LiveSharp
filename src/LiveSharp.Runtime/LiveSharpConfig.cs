using System.Collections.Generic;

namespace LiveSharp.Runtime
{
    public class LiveSharpConfig : ILiveSharpConfig
    {
        private readonly Dictionary<string, string> _settings = new Dictionary<string, string>();
        
        public bool TryGetValue(string key, out string value)
        {
            return _settings.TryGetValue(key, out value);
        }

        public void SetValue(string key, string value)
        {
            _settings[key] = value;
        }
    }
}