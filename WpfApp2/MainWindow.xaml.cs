#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;

using Button = System.Windows.Controls.Button;
using MenuItem = System.Windows.Controls.MenuItem;

namespace WpfApp2
{
    public partial class MainWindow : FluentWindow
    {
        private SteamService _steamService = new SteamService();
        private SettingsService _settingsService = new SettingsService();
        private GameExecutor _executor = new GameExecutor();
        private List<SteamGame> _allGames = new List<SteamGame>();
        private SteamGame _selectedGame;

        private bool _showHidden = false;
        private bool _showUtils = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadSavedGames();
        }

        private void LoadSavedGames()
        {
            _allGames = _steamService.LoadGamesFromDisk();
            foreach (var game in _allGames) _executor.SyncBoostingState(game);
            ApplyFilter();
        }

        // --- NAVIGATION & UI ---
        private void NavToLibrary_Click(object sender, RoutedEventArgs e) { LibraryView.Visibility = Visibility.Visible; SettingsView.Visibility = Visibility.Collapsed; }

        private async void NavToSettings_Click(object sender, RoutedEventArgs e)
        {
            LibraryView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            TxtApiKey.Text = _settingsService.CurrentSettings.SteamApiKey;
            TxtSteamId.Text = _settingsService.CurrentSettings.SteamId64;
            if (!string.IsNullOrEmpty(TxtApiKey.Text)) await CheckApiStatusSilent();
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e) => SidebarGrid.Width = SidebarGrid.Width == 60 ? 200 : 60;
        private void OpenTutorial_Click(object sender, RoutedEventArgs e) => TutorialOverlay.Visibility = Visibility.Visible;
        private void CloseOverlay_Click(object sender, RoutedEventArgs e) { OptionsOverlay.Visibility = Visibility.Collapsed; TutorialOverlay.Visibility = Visibility.Collapsed; NewGamesOverlay.Visibility = Visibility.Collapsed; }

        private void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_settingsService.CurrentSettings.SteamId64))
                Process.Start(new ProcessStartInfo($"https://steamcommunity.com/profiles/{_settingsService.CurrentSettings.SteamId64}") { UseShellExecute = true });
        }

        // --- FILTERING & SORTING ---
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
                SelectedGameId.Text = $"ID: {game.AppId}";
                UpdateBoostButtonState();
                OptionsOverlay.Visibility = Visibility.Visible;
            }
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;
            if (_selectedGame.IsBoosting) { _executor.StopBoosting(_selectedGame); UpdateBoostButtonState(); }
            Process.Start(new ProcessStartInfo { FileName = $"steam://run/{_selectedGame.AppId}", UseShellExecute = true });
        }

        private void BoostGame_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;
            if (_selectedGame.IsBoosting) _executor.StopBoosting(_selectedGame);
            else _executor.StartBoosting(_selectedGame);
            UpdateBoostButtonState();
            _steamService.SaveGamesToDisk(_allGames);
        }

        private void RepairGame_Click(object sender, RoutedEventArgs e) { if (_selectedGame != null) { _executor.RepairGame(_selectedGame); UpdateBoostButtonState(); } }
        private void OpenFolder_Click(object sender, RoutedEventArgs e) { if (_selectedGame != null && System.IO.Directory.Exists(_selectedGame.InstallDir)) Process.Start("explorer.exe", _selectedGame.InstallDir); }

        private void GameSetting_Changed(object sender, RoutedEventArgs e) { _steamService.SaveGamesToDisk(_allGames); ApplyFilter(); }

        // --- SYSTEM ---
        private async void RefreshLibrary_Click(object sender, RoutedEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var newScan = await Task.Run(() => _steamService.GetInstalledGames());
            string key = _settingsService.CurrentSettings.SteamApiKey;
            string id = _settingsService.CurrentSettings.SteamId64;
            if (!string.IsNullOrEmpty(key)) await _steamService.UpdateHoursFromApi(key, id, newScan);

            _allGames = newScan;
            _steamService.SaveGamesToDisk(_allGames);
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
            bool isValid = await _steamService.CheckApiCredentials(TxtApiKey.Text, TxtSteamId.Text);
            TxtApiStatus.Text = isValid ? "API Connected" : "Connection Failed";
            TxtApiStatus.Foreground = isValid ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
        }

        private void UpdateBoostButtonState()
        {
            if (_selectedGame == null) return;
            TxtBoost.Text = _selectedGame.IsBoosting ? "STOP BOOSTING" : "START IDLE BOOST";
            BtnBoost.Background = _selectedGame.IsBoosting ? new SolidColorBrush(Color.FromRgb(200, 40, 40)) : new SolidColorBrush(Color.FromRgb(76, 194, 255));
        }

        private void GitHubUpdate_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/") { UseShellExecute = true });
        private void OpenGitHub_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo("https://github.com/") { UseShellExecute = true });
    }
}