#nullable disable
using System;
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
            // 1. Identify Paths
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
                // 2. SAFETY CHECK: If _real.exe already exists, we might be in a bad state.
                // We shouldn't overwrite our backup with a dummy!
                if (File.Exists(realPath))
                {
                    // The backup exists, meaning the current 'exePath' is likely the dummy.
                    // We proceed to launch, assuming files are already swapped.
                }
                else
                {
                    // Perform the Swap
                    if (File.Exists(exePath))
                    {
                        File.Move(exePath, realPath); // Rename original to _real.exe
                        File.Copy(Path.Combine(Environment.SystemDirectory, "cmd.exe"), exePath, true); // Put dummy in place
                    }
                }

                // 3. Launch via Steam
                Process.Start(new ProcessStartInfo { FileName = $"steam://run/{game.AppId}", UseShellExecute = true });

                game.IsBoosting = true;

                // 4. Watch for the dummy process to close
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
            if (!isResume) await Task.Delay(8000); // Give Steam time to launch

            while (game.IsBoosting)
            {
                await Task.Delay(3000);

                // If the dummy process is gone, we MUST revert files immediately.
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
                // 1. Kill the dummy process if it's still running
                foreach (var p in Process.GetProcessesByName(procName)) { try { p.Kill(); } catch { } }

                // Wait for file lock to release
                Thread.Sleep(500);

                // 2. DISK CHECK REPAIR (The Fix for VAC errors)
                // We don't care if 'game.IsBoosting' is true or false. 
                // If '_real.exe' exists on the disk, we MUST put it back.
                if (File.Exists(realPath))
                {
                    if (File.Exists(exePath)) File.Delete(exePath); // Delete dummy
                    File.Move(realPath, exePath); // Restore original
                }

                game.IsBoosting = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stop boost error: {ex.Message}");
            }
        }

        public void RepairGame(SteamGame game) => StopBoosting(game);

        public void SyncBoostingState(SteamGame game)
        {
            if (string.IsNullOrEmpty(game.InstallDir)) return;

            // Check if files are swapped
            bool isSwapped = Directory.GetFiles(game.InstallDir, "*_real.exe", SearchOption.AllDirectories).Any();

            if (isSwapped)
            {
                string exePath = ResolveExecutablePath(game);
                string processName = !string.IsNullOrEmpty(exePath) ? Path.GetFileNameWithoutExtension(exePath) : null;

                // Check if process is actually running
                bool isActuallyRunning = false;
                if (processName != null)
                {
                    var running = Process.GetProcessesByName(processName);
                    isActuallyRunning = running.Length > 0;
                }

                if (isActuallyRunning)
                {
                    // It is validly running
                    game.IsBoosting = true;
                    WatchProcess(game, processName, isResume: true);
                }
                else
                {
                    // Files swapped but not running? This is a "stuck" state.
                    // Revert immediately so user sees "Start Boost" instead of "Running"
                    StopBoosting(game);
                    game.IsBoosting = false;
                }
            }
            else
            {
                game.IsBoosting = false;
            }
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