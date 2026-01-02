#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace WpfApp2
{
    public class GameExecutor
    {
        public void StartBoosting(SteamGame game)
        {
            string exePath = ResolveExecutablePath(game);
            if (string.IsNullOrEmpty(exePath))
            {
                MessageBox.Show($"Could not find executable for {game.Name}.\nPlease click 'Browse Exe' to set it manually.",
                    "Executable Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string dir = Path.GetDirectoryName(exePath);
            string exeName = Path.GetFileName(exePath);
            string realPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(exeName) + "_real.exe");

            try
            {
                // Safety: If _real.exe exists, we assume the swap is already done.
                if (!File.Exists(realPath))
                {
                    if (File.Exists(exePath))
                    {
                        // Retry policy for file locking issues
                        SafeMove(exePath, realPath);
                        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), exePath, true);
                    }
                }

                // Launch via Steam
                Process.Start(new ProcessStartInfo { FileName = $"steam://run/{game.AppId}", UseShellExecute = true });
                game.IsBoosting = true;

                // Watch process
                string processName = Path.GetFileNameWithoutExtension(exePath);
                WatchProcess(game, processName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start boost: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopBoosting(game); // Revert on failure
            }
        }

        private async void WatchProcess(SteamGame game, string processName, bool isResume = false)
        {
            if (!isResume) await Task.Delay(10000); // Give Steam plenty of time to launch

            while (game.IsBoosting)
            {
                await Task.Delay(3000);

                // If app is closing, break loop
                if (Application.Current == null) break;

                // If dummy process is gone, revert.
                var running = Process.GetProcessesByName(processName);
                if (running.Length == 0)
                {
                    Application.Current.Dispatcher.Invoke(() => StopBoosting(game));
                    break;
                }
            }
        }

        public void StopBoosting(SteamGame game)
        {
            string exePath = ResolveExecutablePath(game);
            if (string.IsNullOrEmpty(exePath)) return;

            string dir = Path.GetDirectoryName(exePath);
            string exeName = Path.GetFileName(exePath);
            string realPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(exeName) + "_real.exe");
            string procName = Path.GetFileNameWithoutExtension(exeName);

            try
            {
                // Kill dummy process
                foreach (var p in Process.GetProcessesByName(procName)) { try { p.Kill(); } catch { } }
                Thread.Sleep(500);

                // Restore file
                if (File.Exists(realPath))
                {
                    if (File.Exists(exePath)) File.Delete(exePath);
                    SafeMove(realPath, exePath);
                }

                game.IsBoosting = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stop boost error: {ex.Message}");
            }
        }

        // --- STABILITY FIXES ---

        /// <summary>
        /// Called on startup. Scans all games for "_real.exe" files. 
        /// If found, it means the app crashed last time and left the game broken.
        /// We must restore it immediately.
        /// </summary>
        public void RestoreIntegrity(List<SteamGame> games)
        {
            foreach (var game in games)
            {
                if (string.IsNullOrEmpty(game.InstallDir)) continue;

                try
                {
                    // Look for any _real.exe in the game folder
                    var swappedFiles = Directory.GetFiles(game.InstallDir, "*_real.exe", SearchOption.AllDirectories);
                    foreach (var realPath in swappedFiles)
                    {
                        string dir = Path.GetDirectoryName(realPath);
                        string fileName = Path.GetFileName(realPath);
                        // Original name is "Game_real.exe" -> "Game.exe"
                        string originalName = fileName.Replace("_real.exe", ".exe");
                        string originalPath = Path.Combine(dir, originalName);

                        Debug.WriteLine($"Restoring broken game file: {originalName}");

                        // Kill any lingering cmd.exe that might be pretending to be the game
                        string procName = Path.GetFileNameWithoutExtension(originalName);
                        foreach (var p in Process.GetProcessesByName(procName)) { try { p.Kill(); } catch { } }

                        if (File.Exists(originalPath)) File.Delete(originalPath);
                        SafeMove(realPath, originalPath);
                    }
                    game.IsBoosting = false;
                }
                catch { }
            }
        }

        public void RepairGame(SteamGame game) => StopBoosting(game);

        public void SyncBoostingState(SteamGame game)
        {
            // We assume startup integrity check has run, so games shouldn't be boosting on load.
            game.IsBoosting = false;
        }

        private void SafeMove(string source, string dest)
        {
            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    File.Move(source, dest);
                    return;
                }
                catch (IOException)
                {
                    retries--;
                    Thread.Sleep(1000); // Wait for file lock
                }
            }
            // Final attempt
            File.Move(source, dest);
        }

        private string ResolveExecutablePath(SteamGame game)
        {
            if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath)) return game.ExecutablePath;
            string dbPath = GameDatabase.GetExePath(game.AppId);
            if (!string.IsNullOrEmpty(dbPath))
            {
                string fullPath = Path.Combine(game.InstallDir, dbPath);
                if (File.Exists(fullPath)) return fullPath;
            }
            return FindGameExecutable(game.InstallDir);
        }

        private string FindGameExecutable(string installDir)
        {
            try
            {
                if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir)) return null;
                return Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories)
                    .Where(x => !x.ToLower().Contains("crash") && !x.ToLower().Contains("unity") && !x.ToLower().Contains("launcher") && !x.ToLower().Contains("redist"))
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();
            }
            catch { return null; }
        }
    }
}