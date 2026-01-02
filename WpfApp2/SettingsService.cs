using System;
using System.IO;
using System.Text.Json;

namespace WpfApp2
{
    public class AppSettings
    {
        public string SteamApiKey { get; set; } = string.Empty;
        public string SteamId64 { get; set; } = string.Empty;
        public bool EnableMica { get; set; } = true; // Renamed from EnableBlur
    }

    public class SettingsService
    {
        private const string SettingsFile = "settings.json";
        public AppSettings CurrentSettings { get; private set; } = new AppSettings();

        public SettingsService()
        {
            LoadSettings();
        }

        public void LoadSettings()
        {
            try
            {
                // Ensure we look in the app's base directory
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFile);

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        CurrentSettings = loaded;
                    }
                }
            }
            catch
            {
                // If loading fails, keep default new AppSettings() to prevent crash
            }
        }

        public void SaveSettings()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFile);
                string json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}