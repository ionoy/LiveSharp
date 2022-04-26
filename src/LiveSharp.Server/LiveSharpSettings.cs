using System;
using System.IO;
using System.Text.Json;

namespace LiveSharp.Server
{
    public class LiveSharpSettings
    {
        private SettingsStore<LiveSharpSettings> _store;

        public static LiveSharpSettings Load()
        {
            var store = new SettingsStore<LiveSharpSettings>("livesharp.settings");
            var settings = store.LoadSettings();
            settings._store = store;
            return settings;
        }

        public void Save()
        {
            _store.SaveSettings(this);
        }
        
        class SettingsStore<T> where T : class, new()
        {
            private readonly string _filename;

            public SettingsStore(string filename)
            {
                _filename = GetLocalFilePath(filename);
            }

            private string GetLocalFilePath(string fileName)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, fileName);
            }

            public T LoadSettings()
            {
                if (File.Exists(_filename))
                    return JsonSerializer.Deserialize<T>(File.ReadAllText(_filename));
            
                return new T();
            }

            public void SaveSettings(T settings)
            {
                string json = JsonSerializer.Serialize(settings);
                File.WriteAllText(_filename, json);
            }
        }
    }
}