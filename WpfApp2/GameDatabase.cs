using System.Collections.Generic;

namespace WpfApp2
{
    public static class GameDatabase
    {
        // manually mapped executables for games where auto-detection might fail or pick the wrong exe (e.g. launchers)
        private static readonly Dictionary<int, string> _exePaths = new Dictionary<int, string>
        {
            { 251570, "7DaysToDie.exe" },
            { 1172470, "r5apex.exe" },
            { 2399830, "ShooterGame\\Binaries\\Win64\\ArkAscended.exe" },
            { 346110, "ShooterGame\\Binaries\\Win64\\ShooterGame.exe" },
            { 805550, "AC2\\Binaries\\Win64\\AC2-Win64-Shipping.exe" },
            { 1086940, "bin\\bg3_dx11.exe" },
            { 284160, "Bin64\\BeamNG.drive.x64.exe" },
            { 582660, "bin64\\BlackDesert64.exe" },
            { 397540, "OakGame\\Binaries\\Win64\\OakGame-Win64-Shipping.exe" },
            { 440900, "ConanSandbox\\Binaries\\Win64\\ConanSandbox.exe" },
            { 730, "game\\bin\\win64\\cs2.exe" },
            { 1091500, "bin\\x64\\Cyberpunk2077.exe" },
            { 374320, "Game\\DarkSoulsIII.exe" },
            { 381210, "DeadByDaylight\\Binaries\\Win64\\DeadByDaylight-Win64-Shipping.exe" },
            { 548430, "FSD\\Binaries\\Win64\\FSD-Win64-Shipping.exe" },
            { 1085660, "Destiny2.exe" },
            { 570, "game\\bin\\win64\\dota2.exe" },
            { 1245620, "Game\\eldenring.exe" },
            { 227300, "bin\\win_x64\\eurotrucks2.exe" },
            { 271590, "GTA5.exe" },
            { 686810, "HLL\\Binaries\\Win64\\HLL-Win64-Shipping.exe" },
            { 553850, "bin\\helldivers2.exe" },
            { 990080, "Phoenix\\Binaries\\Win64\\HogwartsLegacy.exe" },
            { 594650, "bin\\win_x64\\HuntGame.exe" },
            { 1599340, "EFGame\\Binaries\\Win64\\EFGame.exe" },
            { 261550, "bin\\Win64_Shipping_Client\\Bannerlord.exe" },
            { 275850, "Binaries\\Binaries\\NMS.exe" },
            { 1623730, "Pal\\Binaries\\Win64\\Palworld-Win64-Shipping.exe" },
            { 1272080, "PAYDAY3\\Binaries\\Win64\\PAYDAY3Client-Win64-Shipping.exe" },
            { 578080, "TslGame\\Binaries\\Win64\\TslGame.exe" },
            { 359550, "RainbowSix.exe" },
            { 1282100, "Remnant2\\Binaries\\Win64\\Remnant2-Win64-Shipping.exe" },
            { 252950, "Binaries\\Win64\\RocketLeague.exe" },
            { 252490, "RustClient.exe" },
            { 526870, "FactoryGame\\Binaries\\Win64\\FactoryGame-Win64-Shipping.exe" },
            { 1172620, "Athena\\Binaries\\Win64\\SoTGame.exe" },
            { 2183900, "client_pc\\root\\bin\\pc\\SpaceMarine2.exe" },
            { 393380, "SquadGame\\Binaries\\Win64\\SquadGame.exe" },
            { 440, "hl2.exe" },
            { 1778820, "Polaris\\Binaries\\Win64\\Polaris-Win64-Shipping.exe" },
            { 105600, "Terraria.exe" },
            { 2073850, "Discovery\\Binaries\\Win64\\Discovery.exe" },
            { 292030, "bin\\x64\\witcher3.exe" },
            { 236390, "win64\\aces.exe" },
            { 230410, "Warframe.x64.exe" },
            { 413150, "Stardew Valley.exe" },
            { 4000, "hl2.exe" }, // Garry's Mod
            { 431960, "wallpaper32.exe" } // Wallpaper Engine
        };

        public static string GetExePath(int appId)
        {
            return _exePaths.TryGetValue(appId, out var path) ? path : string.Empty;
        }
    }
}