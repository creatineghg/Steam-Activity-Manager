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
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WpfApp2
{
    public class SteamService
    {
        private const string SaveFile = "games.json";
        private readonly HttpClient _httpClient = new HttpClient();

        public bool IsSteamRunning()
        {
            return Process.GetProcessesByName("steam").Length > 0;
        }

        public async Task<List<SteamGame>> GetInstalledGamesAsync()
        {
            return await Task.Run(() =>
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
            });
        }

        private SteamGame ParseManifest(string filePath, string libraryPath)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                dynamic appState = VdfConvert.Deserialize(content).Value;

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

        private string GetSteamPath()
        {
            string path = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null) as string;
            if (!string.IsNullOrEmpty(path)) return path;

            path = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
            if (!string.IsNullOrEmpty(path)) return path;

            path = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (!string.IsNullOrEmpty(path)) return path.Replace("/", "\\");

            string[] commonPaths = { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam", @"D:\Steam", @"E:\Steam" };
            foreach (var p in commonPaths) if (Directory.Exists(p)) return p;

            return null;
        }

        private HashSet<string> GetLibraryFolders(string steamPath)
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };
            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

            if (File.Exists(vdfPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(vdfPath);
                    foreach (var line in lines)
                    {
                        var match = Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
                        if (match.Success)
                        {
                            string p = match.Groups[1].Value.Replace("\\\\", "\\");
                            if (Directory.Exists(p)) folders.Add(p);
                        }
                    }
                }
                catch { }
            }
            return folders;
        }

        public async Task UpdateHoursFromApi(string key, string steamId, List<SteamGame> games)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(steamId)) return;

            try
            {
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
                        if (localGame != null) localGame.PlaytimeMinutes = playtime;
                    }
                }
            }
            catch { }
        }

        public async Task UpdateAllAchievementsCount(string key, string steamId, List<SteamGame> games)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(steamId)) return;

            var options = new ParallelOptions { MaxDegreeOfParallelism = 5 };
            await Parallel.ForEachAsync(games, options, async (game, token) =>
            {
                await LoadAchievementCount(key, steamId, game);
            });
        }

        private async Task LoadAchievementCount(string key, string steamId, SteamGame game)
        {
            try
            {
                string url = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={game.AppId}&key={key}&steamid={steamId}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                var node = JsonNode.Parse(json);
                var stats = node?["playerstats"];

                if (stats != null && (bool)(stats["success"] ?? false))
                {
                    var achievements = stats["achievements"]?.AsArray();
                    if (achievements != null)
                    {
                        game.AchievementsTotal = achievements.Count;
                        game.AchievementsEarned = achievements.Count(a => (int)(a["achieved"] ?? 0) == 1);
                    }
                }
            }
            catch { }
        }

        public async Task<List<SteamAchievement>> GetDetailedAchievements(string key, string steamId, int appId)
        {
            var list = new List<SteamAchievement>();
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(steamId)) return list;

            try
            {
                // Use Case-Insensitive Dictionary to map percentages
                var globalPercents = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string globalUrl = $"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={appId}";
                    var globalJson = await _httpClient.GetStringAsync(globalUrl);
                    var globalNode = JsonNode.Parse(globalJson);
                    var globalArr = globalNode?["achievementpercentages"]?["achievements"]?.AsArray();
                    if (globalArr != null)
                    {
                        foreach (var item in globalArr)
                        {
                            string name = item["name"]?.ToString();
                            double pct = (double)(item["percent"] ?? 0);
                            if (!string.IsNullOrEmpty(name)) globalPercents[name] = pct;
                        }
                    }
                }
                catch { }

                var schemaDict = new Dictionary<string, SteamAchievement>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string schemaUrl = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={key}&appid={appId}";
                    var schemaJson = await _httpClient.GetStringAsync(schemaUrl);
                    var schemaNode = JsonNode.Parse(schemaJson);
                    var available = schemaNode?["game"]?["availableGameStats"]?["achievements"]?.AsArray();

                    if (available != null)
                    {
                        foreach (var item in available)
                        {
                            string apiName = item["name"]?.ToString();
                            if (string.IsNullOrEmpty(apiName)) continue;

                            var ach = new SteamAchievement
                            {
                                ApiName = apiName,
                                DisplayName = item["displayName"]?.ToString() ?? apiName,
                                Description = item["description"]?.ToString() ?? "",
                                IconUrl = item["icon"]?.ToString() ?? "",
                                IconGrayUrl = item["icongray"]?.ToString() ?? "",
                                Achieved = false
                            };

                            // Map percentage immediately
                            if (globalPercents.TryGetValue(apiName, out double val))
                                ach.GlobalPercent = val;

                            schemaDict[apiName] = ach;
                        }
                    }
                }
                catch { }

                string userUrl = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={appId}&key={key}&steamid={steamId}";
                var userJson = await _httpClient.GetStringAsync(userUrl);
                var userNode = JsonNode.Parse(userJson);
                var userAchievements = userNode?["playerstats"]?["achievements"]?.AsArray();

                if (userAchievements != null)
                {
                    foreach (var item in userAchievements)
                    {
                        string apiName = item["apiname"]?.ToString();
                        int achieved = (int)(item["achieved"] ?? 0);

                        if (apiName == null) continue;

                        if (schemaDict.TryGetValue(apiName, out var ach))
                        {
                            ach.Achieved = achieved == 1;
                            list.Add(ach);
                        }
                        else
                        {
                            double pct = 0;
                            if (globalPercents.TryGetValue(apiName, out double val)) pct = val;

                            list.Add(new SteamAchievement
                            {
                                ApiName = apiName,
                                DisplayName = apiName,
                                Description = "Hidden or No Schema",
                                Achieved = achieved == 1,
                                GlobalPercent = pct
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error fetching details: {ex.Message}");
            }

            return list.OrderByDescending(x => x.Achieved).ThenBy(x => x.DisplayName).ToList();
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

        public async Task SaveGamesToDiskAsync(List<SteamGame> games)
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(SaveFile));
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(games, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(SaveFile, json);
            }
            catch (Exception ex) { Debug.WriteLine($"Save failed: {ex.Message}"); }
        }

        public async Task<List<SteamGame>> LoadGamesFromDiskAsync()
        {
            if (!File.Exists(SaveFile)) return new List<SteamGame>();
            try
            {
                string json = await File.ReadAllTextAsync(SaveFile);
                return JsonSerializer.Deserialize<List<SteamGame>>(json) ?? new List<SteamGame>();
            }
            catch { return new List<SteamGame>(); }
        }
    }
}