# Steam Activity Manager (v0.3)

**Steam Activity Manager** is a modern, lightweight utility for Windows designed to manage your Steam game activity. It allows you to log playtime hours for any installed game without keeping the actual heavy game executable running, using a resource-efficient method.

## 🌟 Key Features

* **Efficient Activity Tracking**: Log hours with ~0% CPU/GPU usage by running a lightweight dummy process instead of the full game.
* **Safe & Reversible**: Automatically handles file swapping and restoration.
* **Auto-Restoration**: Detects when you want to play the real game and restores original files instantly.
* **Library Management**: Filter games, view hidden ones, and mark utilities.
* **Modern UI**: Sleek, dark-themed interface built with WPF UI.

## 🛠️ Setup Guide (Step-by-Step)

To get the most out of Steam Activity Manager (like accurate playtime syncing), you need to configure your API credentials.

### 1. Get your Steam Web API Key
This key allows the app to read your game list and current playtime from Steam's servers.
1.  Go to the [Steam Web API Key page](https://steamcommunity.com/dev/apikey).
2.  Sign in with your Steam account.
3.  In the "Domain Name" field, you can enter anything (e.g., `localhost` or `my-pc`).
4.  Copy the **Key** generated (it looks like a long string of numbers and letters).

### 2. Find your SteamID64
This is your unique 17-digit user identifier.
1.  Go to [SteamDB Calculator](https://steamdb.info/calculator/) (or any Steam ID finder).
2.  Enter your Steam Profile URL or username.
3.  Look for the **SteamID** field (it is a number that starts with `7656...`).
4.  Copy that number.

### 3. Configure the App
1.  Open **Steam Activity Manager**.
2.  Click **Settings** in the sidebar.
3.  Paste your **Web API Key** and **SteamID64** into the respective fields.
4.  Click **Verify & Save**. If the status turns green ("Connected"), you are good to go!

## 🚀 How to Use

### Starting Activity (Idling)
1.  Click **Refresh Games** to load your library.
2.  Select a game from the list.
3.  Click **START ACTIVITY**.
    * *The status will change to "ACTIVE" and Steam will show you as "In-Game".*

### Playing the Real Game
1.  Select the game in the menu.
2.  Click **PLAY**.
    * *The app automatically stops the activity, restores the original game files, and launches the real game via Steam.*

## ⚠️ Important Notes

* **Safety**: This tool uses a file-swapping method (replacing the game exe with a lightweight cmd). While generally safe, **use at your own risk**.
* **VAC Games**: We recommend **stopping activity** manually before launching any VAC-secured multiplayer game to ensure all files are verified correctly before the anti-cheat loads.

## 📦 Installation

1.  Download the latest release from the [GitHub Releases](https://github.com/creatineghg/Steam-Hour-Manager/releases).
2.  Extract the zip file.
3.  Run `SteamActivityManager.exe`.
4.  Ensure Steam is running.

---
*Created by [CreatineGHG](https://github.com/creatineghg/Steam-Hour-Manager)*
