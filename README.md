# 🚀 Steam Hour Manager

A lightweight WPF application designed to manage and idle your Steam game hours using a exe-swap method.

## ✨ Features
- **Library Scan:** Automatically finds all your installed Steam games.
- **Idling:** Renames the game's original executable and replaces it with a cmd dummy to track hours with near 0% usage.
- **API Integration:** Connect your Steam Web API to sync real-time playtime directly into the app.
- **UI:** Looks nice.

## 🛠️ Requirements
- .NET 10.0
- Steam 

## 🚀 Quick Start
1. **Refresh Games:** Click the refresh button to scan your local Steam libraries.
2. **Setup API:** (Optional) Add your Steam API Key and SteamID64 in Settings for accurate hour tracking.
3. **Start Boosting Hours:** Select a game and click "Start Idle Boost".
4. **Play:** Always click "Stop Boost" before actually playing the game to restore the original files.

## ⚠️ Safety Hint
If a game doesn't launch or you closed the app unexpectedly, use the **'Repair Files'** button in the game options to restore the original `.exe`.
