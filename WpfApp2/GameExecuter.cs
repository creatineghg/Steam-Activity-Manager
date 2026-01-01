#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WpfApp2
{
    public class GameExecutor
    {
        public void StartBoosting(SteamGame game)
        {
            string exePath = ResolveExecutablePath(game);
            if (string.IsNullOrEmpty(exePath)) return;

            string dir = Path.GetDirectoryName(exePath);
            string exeName = Path.GetFileName(exePath);
            string realPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(exeName) + "_real.exe");

            try
            {
                if (!File.Exists(realPath)) File.Move(exePath, realPath);
                if (!File.Exists(exePath)) File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), exePath, true);

                Process p = Process.Start(new ProcessStartInfo { FileName = $"steam://run/{game.AppId}", UseShellExecute = true });

                game.IsBoosting = true;

                // Monitor the process: If the user closes the CMD window, reset the UI
                WatchProcess(game, Path.GetFileNameWithoutExtension(exeName));
            }
            catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); StopBoosting(game); }
        }

        private async void WatchProcess(SteamGame game, string processName)
        {
            while (game.IsBoosting)
            {
                await Task.Delay(3000); // Check every 3 seconds
                var running = Process.GetProcessesByName(processName);
                if (running.Length == 0)
                {
                    // Process was closed manually by user
                    StopBoosting(game);
                    break;
                }
            }
        }

        public void StopBoosting(SteamGame game)
        {
            if (string.IsNullOrEmpty(game.InstallDir)) return;
            string exePath = ResolveExecutablePath(game);
            if (string.IsNullOrEmpty(exePath)) return;

            string dir = Path.GetDirectoryName(exePath);
            string exeName = Path.GetFileName(exePath);
            string realPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(exeName) + "_real.exe");

            try
            {
                foreach (var p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exeName)))
                {
                    try { p.Kill(); } catch { }
                }

                System.Threading.Thread.Sleep(500);
                if (File.Exists(realPath))
                {
                    if (File.Exists(exePath)) File.Delete(exePath);
                    File.Move(realPath, exePath);
                }
                game.IsBoosting = false;
            }
            catch { }
        }

        public void RepairGame(SteamGame game) => StopBoosting(game);

        public void SyncBoostingState(SteamGame game)
        {
            if (string.IsNullOrEmpty(game.InstallDir)) return;
            game.IsBoosting = Directory.GetFiles(game.InstallDir, "*_real.exe", SearchOption.AllDirectories).Any();
        }

        private string ResolveExecutablePath(SteamGame game)
        {
            if (!string.IsNullOrEmpty(game.ExecutablePath) && File.Exists(game.ExecutablePath)) return game.ExecutablePath;
            return FindGameExecutable(game.InstallDir);
        }

        private string FindGameExecutable(string installDir)
        {
            try
            {
                if (!Directory.Exists(installDir)) return null;
                return Directory.GetFiles(installDir, "*.exe", SearchOption.AllDirectories)
                    .Where(x => !x.ToLower().Contains("crash") && !x.ToLower().Contains("launcher"))
                    .OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            }
            catch { return null; }
        }
    }
}