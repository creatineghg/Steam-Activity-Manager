#nullable disable
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WpfApp2
{
    public class SteamService
    {
        private const string SaveFile = "games.json";
        private readonly HttpClient _httpClient = new HttpClient();

        public List<SteamGame> GetInstalledGames()
        {
            var games = new List<SteamGame>();
            string steamPath = GetSteamPath();

            if (string.IsNullOrEmpty(steamPath)) return games;

            var libraryFolders = GetLibraryFolders(steamPath);
            foreach (var libFolder in libraryFolders)
            {
                string steamAppsPath = Path.Combine(libFolder, "steamapps");
                if (!Directory.Exists(steamAppsPath)) continue;

                foreach (var file in Directory.GetFiles(steamAppsPath, "appmanifest_*.acf"))
                {
                    try
                    {
                        var game = ParseManifest(file, libFolder);
                        if (game != null && !games.Any(g => g.AppId == game.AppId))
                            games.Add(game);
                    }
                    catch { }
                }
            }
            return games.OrderBy(g => g.Name).ToList();
        }

        private SteamGame ParseManifest(string filePath, string libraryPath)
        {
            try
            {
                dynamic appState = VdfConvert.Deserialize(File.ReadAllText(filePath)).Value;
                string folderName = appState.installdir.ToString();
                string fullInstallDir = Path.Combine(libraryPath, "steamapps", "common", folderName);

                return new SteamGame
                {
                    AppId = int.Parse(appState.appid.ToString()),
                    Name = appState.name.ToString() ?? "Unknown",
                    InstallDir = fullInstallDir
                };
            }
            catch { return null; }
        }

        // --- API UPDATER ---
        public async Task UpdateHoursFromApi(string key, string steamId, List<SteamGame> games)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(steamId)) return;

            try
            {
                // Force HTTPS
                string url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={key}&steamid={steamId}&format=json&include_played_free_games=1";
                string json = await _httpClient.GetStringAsync(url);

                var node = JsonNode.Parse(json);
                var gamesArray = node?["response"]?["games"]?.AsArray();

                if (gamesArray != null)
                {
                    foreach (var item in gamesArray)
                    {
                        int appId = (int)(item["appid"] ?? 0);
                        int playtime = (int)(item["playtime_forever"] ?? 0);

                        var localGame = games.FirstOrDefault(g => g.AppId == appId);
                        if (localGame != null)
                        {
                            localGame.PlaytimeMinutes = playtime;
                        }
                    }
                }
            }
            catch { }
        }

        public void RefreshPlaytime(List<SteamGame> games, string steamPath = null)
        {
            if (steamPath == null) steamPath = GetSteamPath();
            if (string.IsNullOrEmpty(steamPath)) return;

            try
            {
                string userDataPath = Path.Combine(steamPath, "userdata");
                if (Directory.Exists(userDataPath))
                {
                    foreach (var userDir in Directory.GetDirectories(userDataPath))
                    {
                        string configPath = Path.Combine(userDir, "config", "localconfig.vdf");
                        if (File.Exists(configPath)) ApplyPlaytimeStats(configPath, games);
                    }
                }
            }
            catch { }
        }

        private void ApplyPlaytimeStats(string configPath, List<SteamGame> games)
        {
            try
            {
                var content = File.ReadAllText(configPath);
                var vdf = VdfConvert.Deserialize(content);
                dynamic root = vdf.Value;
                var apps = root?.Software?.Valve?.Steam?.apps;
                if (apps == null) return;

                foreach (var child in apps)
                {
                    if (child is VProperty appProp && int.TryParse(appProp.Key, out int appId))
                    {
                        var game = games.FirstOrDefault(g => g.AppId == appId);
                        if (game != null)
                        {
                            dynamic appData = appProp.Value;
                            string timeStr = null;
                            try { timeStr = appData["PlayTime"]?.ToString(); } catch { }
                            if (timeStr == null) try { timeStr = appData["playTime"]?.ToString(); } catch { }

                            if (int.TryParse(timeStr, out int minutes) && minutes > game.PlaytimeMinutes)
                                game.PlaytimeMinutes = minutes;
                        }
                    }
                }
            }
            catch { }
        }

        public async Task<bool> CheckApiCredentials(string key, string steamId)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(steamId)) return false;
            try
            {
                string url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={key}&steamids={steamId}";
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public void SaveGamesToDisk(List<SteamGame> games)
        {
            try { File.WriteAllText(SaveFile, JsonSerializer.Serialize(games)); } catch { }
        }

        public List<SteamGame> LoadGamesFromDisk()
        {
            if (!File.Exists(SaveFile)) return new List<SteamGame>();
            try
            {
                return JsonSerializer.Deserialize<List<SteamGame>>(File.ReadAllText(SaveFile)) ?? new List<SteamGame>();
            }
            catch { return new List<SteamGame>(); }
        }

        private string GetSteamPath()
        {
            object regVal64 = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null);
            string regPath = regVal64 as string;
            if (regPath == null)
            {
                object regVal32 = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
                regPath = regVal32 as string;
            }
            return regPath?.Replace("/", "\\");
        }

        private HashSet<string> GetLibraryFolders(string steamPath)
        {
            var folders = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { steamPath };
            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdfPath))
            {
                try
                {
                    var vdf = VdfConvert.Deserialize(File.ReadAllText(vdfPath));
                    foreach (var child in vdf.Value)
                    {
                        if (child is VProperty prop && prop.Value is VObject obj)
                        {
                            var pathToken = obj["path"];
                            if (pathToken != null) folders.Add(pathToken.ToString());
                        }
                    }
                }
                catch { }
            }
            return folders;
        }
    }
}