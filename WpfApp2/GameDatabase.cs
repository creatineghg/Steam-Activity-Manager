using System.Collections.Generic;

namespace WpfApp2
{
    public static class GameDatabase
    {
        public static readonly Dictionary<int, string> KnownGames = new Dictionary<int, string>
        {
            { 730, "game\\bin\\win64\\cs2.exe" },
            { 570, "game\\bin\\win64\\dota2.exe" },
            { 271590, "GTA5.exe" },
            { 1172470, "r5apex.exe" },
            { 578080, "TslGame\\Binaries\\Win64\\TslGame.exe" },
            { 252490, "RustClient.exe" },
            { 440, "hl2.exe" },
            { 359550, "RainbowSix.exe" },
            { 230410, "Warframe.x64.exe" },
            { 292030, "TheWitcher3.exe" },
            { 1091500, "Cyberpunk2077.exe" },
            { 105600, "Terraria.exe" },
            { 251570, "7DaysToDie.exe" },
            { 1085660, "Destiny2.exe" },
            { 236390, "WarThunder.exe" }
        };

        // FIX: Added '?' to string to allow null returns
        public static string? GetExePath(int appId)
        {
            if (KnownGames.ContainsKey(appId))
            {
                return KnownGames[appId];
            }
            return null; // This is now allowed
        }
    }
}