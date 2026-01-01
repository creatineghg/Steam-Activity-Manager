#nullable disable
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;

using Button = System.Windows.Controls.Button;
using MenuItem = System.Windows.Controls.MenuItem;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace WpfApp2
{
    public partial class MainWindow : FluentWindow
    {
        private readonly bool FORCE_DEBUG_OFF = false;

        private SteamService _steamService = new SteamService();
        private SettingsService _settingsService = new SettingsService();
        private GameExecutor _executor = new GameExecutor();
        private List<SteamGame> _allGames = new List<SteamGame>();
        private SteamGame _selectedGame;

        private DispatcherTimer _connectionTimer;
        private bool _showHidden = false;
        private bool _showUtils = false;

        public MainWindow()
        {
            InitializeComponent();

            _connectionTimer = new DispatcherTimer();
            _connectionTimer.Interval = TimeSpan.FromSeconds(10);
            _connectionTimer.Tick += ConnectionTimer_Tick;
            _connectionTimer.Start();

            TxtApiStatus.Cursor = Cursors.Hand;
            TxtApiStatus.MouseLeftButtonDown += (s, e) => _ = CheckApiStatusSilent();
            TxtApiStatus.ToolTip = "Click to retry connection";

            if (FORCE_DEBUG_OFF)
            {
                if (ChkDebugMode != null) ChkDebugMode.Visibility = Visibility.Collapsed;
                BtnDebugMenu.Visibility = Visibility.Collapsed;
            }

            LoadSavedGames();
        }

        private async void LoadSavedGames()
        {
            _allGames = await _steamService.LoadGamesFromDiskAsync();
            foreach (var game in _allGames) _executor.SyncBoostingState(game);
            ApplyFilter();
            await CheckApiStatusSilent();
        }

        private async void ConnectionTimer_Tick(object sender, EventArgs e)
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                UpdateApiStatusUI(false, "Offline (Retrying...)");
                return;
            }
            string currentStatus = TxtApiStatus.Text;
            if (currentStatus.Contains("Failed") || currentStatus.Contains("Offline") || currentStatus.Contains("Not Checked"))
            {
                if (!string.IsNullOrEmpty(TxtApiKey.Text)) await CheckApiStatusSilent();
            }
        }

        // --- NAVIGATION ---
        private void NavToLibrary_Click(object sender, RoutedEventArgs e) { LibraryView.Visibility = Visibility.Visible; SettingsView.Visibility = Visibility.Collapsed; }
        private async void NavToSettings_Click(object sender, RoutedEventArgs e)
        {
            LibraryView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            TxtApiKey.Text = _settingsService.CurrentSettings.SteamApiKey;
            TxtSteamId.Text = _settingsService.CurrentSettings.SteamId64;
            ChkDebugMode.IsChecked = BtnDebugMenu.Visibility == Visibility.Visible;
            if (!string.IsNullOrEmpty(TxtApiKey.Text)) await CheckApiStatusSilent();
        }
        private void ToggleSidebar_Click(object sender, RoutedEventArgs e) => SidebarGrid.Width = SidebarGrid.Width == 60 ? 200 : 60;
        private void OpenTutorial_Click(object sender, RoutedEventArgs e) => TutorialOverlay.Visibility = Visibility.Visible;
        private void CloseOverlay_Click(object sender, RoutedEventArgs e) { OptionsOverlay.Visibility = Visibility.Collapsed; TutorialOverlay.Visibility = Visibility.Collapsed; NewGamesOverlay.Visibility = Visibility.Collapsed; DebugOverlay.Visibility = Visibility.Collapsed; }

        private void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_settingsService.CurrentSettings.SteamId64))
                Process.Start(new ProcessStartInfo($"https://steamcommunity.com/profiles/{_settingsService.CurrentSettings.SteamId64}") { UseShellExecute = true });
        }

        // --- FILTERING ---
        private void Filter_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
        private void Sort_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();
        private void FilterMenu_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.ContextMenu != null) { btn.ContextMenu.PlacementTarget = btn; btn.ContextMenu.IsOpen = true; } }
        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                if (item.Header.ToString().Contains("Hidden")) _showHidden = item.IsChecked;
                if (item.Header.ToString().Contains("Utilities")) _showUtils = item.IsChecked;
            }
            ApplyFilter();
        }
        private void ApplyFilter()
        {
            if (GamesList == null) return;
            string search = SearchBox.Text?.ToLower() ?? "";
            var filtered = _allGames.Where(g => (_showHidden || !g.IsHidden) && (_showUtils || !g.IsUtility) && (g.Name.ToLower().Contains(search)));
            if (SortComboBox != null)
            {
                switch (SortComboBox.SelectedIndex)
                {
                    case 0: filtered = filtered.OrderBy(g => g.Name); break;
                    case 1: filtered = filtered.OrderByDescending(g => g.Name); break;
                    case 2: filtered = filtered.OrderByDescending(g => g.PlaytimeMinutes); break;
                    case 3: filtered = filtered.OrderBy(g => g.PlaytimeMinutes); break;
                }
            }
            GamesList.ItemsSource = filtered.ToList();
        }

        // --- GAME ACTIONS ---
        private void Game_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SteamGame game)
            {
                _selectedGame = game;
                SelectedGameTitle.Text = game.Name;
                SelectedGameTime.Text = game.PlaytimeFullString;
                SelectedGameId.Text = $"AppID: {game.AppId}";
                ChkHideGame.IsChecked = game.IsHidden;
                ChkIsUtility.IsChecked = game.IsUtility;
                TxtGamePath.Text = !string.IsNullOrEmpty(game.ExecutablePath) ? game.ExecutablePath : "Auto-Detected";
                UpdateBoostButtonState();
                OptionsOverlay.Visibility = Visibility.Visible;
            }
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;
            _executor.StopBoosting(_selectedGame);
            UpdateBoostButtonState();
            Thread.Sleep(500);
            Process.Start(new ProcessStartInfo { FileName = $"steam://run/{_selectedGame.AppId}", UseShellExecute = true });
        }

        private void BoostGame_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;
            if (_selectedGame.IsBoosting) _executor.StopBoosting(_selectedGame);
            else _executor.StartBoosting(_selectedGame);
            UpdateBoostButtonState();
            _ = _steamService.SaveGamesToDiskAsync(_allGames);
        }

        private void RepairGame_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame != null)
            {
                _executor.RepairGame(_selectedGame);
                MessageBox.Show("Files restored.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                UpdateBoostButtonState();
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e) { if (_selectedGame != null && System.IO.Directory.Exists(_selectedGame.InstallDir)) Process.Start("explorer.exe", _selectedGame.InstallDir); }

        private void OpenStorePage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame != null)
                Process.Start(new ProcessStartInfo($"https://store.steampowered.com/app/{_selectedGame.AppId}") { UseShellExecute = true });
        }

        private void BrowseExe_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;
            var dialog = new OpenFileDialog { Filter = "Executables (*.exe)|*.exe", InitialDirectory = _selectedGame.InstallDir };
            if (dialog.ShowDialog() == true)
            {
                _selectedGame.ExecutablePath = dialog.FileName;
                TxtGamePath.Text = dialog.FileName;
                _ = _steamService.SaveGamesToDiskAsync(_allGames);
            }
        }

        private void GameSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (_selectedGame != null)
            {
                _selectedGame.IsHidden = ChkHideGame.IsChecked ?? false;
                _selectedGame.IsUtility = ChkIsUtility.IsChecked ?? false;
                _ = _steamService.SaveGamesToDiskAsync(_allGames);
                ApplyFilter();
            }
        }

        // --- DEBUG MENU ---
        private void DebugMode_Changed(object sender, RoutedEventArgs e)
        {
            if (FORCE_DEBUG_OFF) return;
            BtnDebugMenu.Visibility = ChkDebugMode.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
        private void OpenDebug_Click(object sender, RoutedEventArgs e) => DebugOverlay.Visibility = Visibility.Visible;

        private void DebugFakeBoost_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame != null)
            {
                _selectedGame.IsBoosting = !_selectedGame.IsBoosting;
                MessageBox.Show($"UI State toggled for {_selectedGame.Name}.", "Debug");
                UpdateBoostButtonState();
            }
        }

        private void DebugReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Reset ALL game files?", "Emergency Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var g in _allGames) _executor.StopBoosting(g);
                MessageBox.Show("Reset complete.");
            }
        }

        private void DebugOpenConfig_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", Environment.CurrentDirectory);
        }

        private void DebugRestart_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            Application.Current.Shutdown();
        }

        // --- SYSTEM ---
        private async void RefreshLibrary_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var oldGamesDict = _allGames.ToDictionary(g => g.AppId);
            var newScan = await _steamService.GetInstalledGamesAsync();
            foreach (var newGame in newScan)
            {
                if (oldGamesDict.TryGetValue(newGame.AppId, out var oldGame))
                {
                    newGame.IsHidden = oldGame.IsHidden;
                    newGame.IsUtility = oldGame.IsUtility;
                    newGame.PlaytimeMinutes = Math.Max(newGame.PlaytimeMinutes, oldGame.PlaytimeMinutes);
                    newGame.ExecutablePath = oldGame.ExecutablePath;
                }
            }
            string key = _settingsService.CurrentSettings.SteamApiKey;
            string id = _settingsService.CurrentSettings.SteamId64;
            if (!string.IsNullOrEmpty(key) && NetworkInterface.GetIsNetworkAvailable()) await _steamService.UpdateHoursFromApi(key, id, newScan);
            var newlyAdded = newScan.Where(g => !oldGamesDict.ContainsKey(g.AppId)).ToList();
            if (newlyAdded.Any()) { NewGamesList.ItemsSource = newlyAdded; NewGamesOverlay.Visibility = Visibility.Visible; }
            _allGames = newScan;
            await _steamService.SaveGamesToDiskAsync(_allGames);
            ApplyFilter();
            Mouse.OverrideCursor = null;
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.CurrentSettings.SteamApiKey = TxtApiKey.Text;
            _settingsService.CurrentSettings.SteamId64 = TxtSteamId.Text;
            _settingsService.SaveSettings();
            await CheckApiStatusSilent();
        }

        private async Task CheckApiStatusSilent()
        {
            if (!NetworkInterface.GetIsNetworkAvailable()) { UpdateApiStatusUI(false, "Offline (Retrying...)"); return; }
            bool isValid = await _steamService.CheckApiCredentials(TxtApiKey.Text, TxtSteamId.Text);
            UpdateApiStatusUI(isValid, isValid ? "Connected" : "Invalid Key/ID");
        }

        private void UpdateApiStatusUI(bool isValid, string text)
        {
            TxtApiStatus.Text = text;
            TxtApiStatus.Foreground = isValid ? new SolidColorBrush(Colors.LightGreen) : new SolidColorBrush(Colors.Red);
        }

        private void UpdateBoostButtonState()
        {
            if (_selectedGame == null) return;
            TxtBoost.Text = _selectedGame.IsBoosting ? "STOP ACTIVITY" : "START ACTIVITY";
            BtnBoost.Background = _selectedGame.IsBoosting ? new SolidColorBrush(Color.FromRgb(200, 40, 40)) : new SolidColorBrush(Color.FromRgb(76, 194, 255));
        }

        private void GitHubUpdate_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/creatineghg/Steam-Hour-Manager") { UseShellExecute = true });
        private void OpenGitHub_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/creatineghg/Steam-Hour-Manager") { UseShellExecute = true });
    }
}