using System.IO;
using System.Text.Json;

namespace WpfApp2
{
    public class AppSettings
    {
        public string SteamApiKey { get; set; } = string.Empty;
        public string SteamId64 { get; set; } = string.Empty;
    }

    public class SettingsService
    {
        private const string SettingsFile = "appsettings.json";

        public AppSettings CurrentSettings { get; private set; } = new AppSettings();

        public SettingsService()
        {
            LoadSettings();
        }

        public void LoadSettings()
        {
            if (!File.Exists(SettingsFile)) return;

            try
            {
                var json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    CurrentSettings = loaded;
                }
            }
            catch
            {
                CurrentSettings = new AppSettings();
            }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}