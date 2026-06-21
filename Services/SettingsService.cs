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
                using (var stream = File.OpenRead(_settingsPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AppSettings));
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
                File.WriteAllText(_settingsPath, json, Encoding.UTF8);
            }
        }
    }
}
