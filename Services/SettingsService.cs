using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using PicDispatch.Models;

namespace PicDispatch.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsDirectory = Path.Combine(appData, "PicDispatch");
            Directory.CreateDirectory(settingsDirectory);
            _settingsPath = Path.Combine(settingsDirectory, "settings.json");
        }

        public AppSettings Load()
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                var serializer = new DataContractJsonSerializer(typeof(AppSettings));
                var bytes = Encoding.UTF8.GetBytes(json);
                using (var stream = new MemoryStream(bytes))
                {
                    return serializer.ReadObject(stream) as AppSettings ?? new AppSettings();
                }
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            var serializer = new DataContractJsonSerializer(typeof(AppSettings));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, settings);
                var json = Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(_settingsPath, json, Utf8NoBom);
            }
        }
    }
}
