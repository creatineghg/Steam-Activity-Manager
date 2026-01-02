#nullable disable
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
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
            UpdateBackdrop();
        }

        private async void LoadSavedGames()
        {
            _allGames = await _steamService.LoadGamesFromDiskAsync();
            ApplyFilter();

            _ = Task.Run(async () =>
            {
                _executor.RestoreIntegrity(_allGames);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var game in _allGames) _executor.SyncBoostingState(game);
                });
                await ScanLibraryAsync(silent: true);
            });

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
                if (!string.IsNullOrEmpty(_settingsService.CurrentSettings.SteamApiKey)) await CheckApiStatusSilent();
            }
        }

        private void UpdateBackdrop()
        {
            if (_settingsService.CurrentSettings.EnableMica)
            {
                this.WindowBackdropType = WindowBackdropType.Mica;
                this.Background = new SolidColorBrush(Colors.Transparent);
            }
            else
            {
                this.WindowBackdropType = WindowBackdropType.None;
                this.Background = new SolidColorBrush(Color.FromRgb(18, 18, 18));
            }
        }

        private void ToggleBlur(bool isVisible)
        {
            if (!_settingsService.CurrentSettings.EnableMica)
            {
                MainContentGrid.Effect = null;
                return;
            }

            if (isVisible)
            {
                MainContentGrid.Effect = new BlurEffect { Radius = 15, KernelType = KernelType.Gaussian };
            }
            else
            {
                MainContentGrid.Effect = null;
            }
        }

        private void NavToLibrary_Click(object sender, RoutedEventArgs e) { LibraryView.Visibility = Visibility.Visible; SettingsView.Visibility = Visibility.Collapsed; }

        private async void NavToSettings_Click(object sender, RoutedEventArgs e)
        {
            LibraryView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;

            PbApiKey.Password = _settingsService.CurrentSettings.SteamApiKey;
            TxtSteamId.Text = _settingsService.CurrentSettings.SteamId64;

            ChkDebugMode.IsChecked = BtnDebugMenu.Visibility == Visibility.Visible;
            ChkMica.IsChecked = _settingsService.CurrentSettings.EnableMica;

            if (!string.IsNullOrEmpty(PbApiKey.Password)) await CheckApiStatusSilent();
        }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e) => SidebarGrid.Width = SidebarGrid.Width == 60 ? 200 : 60;
        private void OpenTutorial_Click(object sender, RoutedEventArgs e)
        {
            TutorialOverlay.Visibility = Visibility.Visible;
            ToggleBlur(true);
        }

        private void CloseOverlay_Click(object sender, RoutedEventArgs e)
        {
            OptionsOverlay.Visibility = Visibility.Collapsed;
            AchievementsOverlay.Visibility = Visibility.Collapsed;
            TutorialOverlay.Visibility = Visibility.Collapsed;
            NewGamesOverlay.Visibility = Visibility.Collapsed;
            DebugOverlay.Visibility = Visibility.Collapsed;
            NoNewGamesOverlay.Visibility = Visibility.Collapsed;

            ToggleBlur(false);
        }

        private void CloseAchievements_Click(object sender, RoutedEventArgs e)
        {
            AchievementsOverlay.Visibility = Visibility.Collapsed;
            if (e is MouseButtonEventArgs mouseArgs) mouseArgs.Handled = true;
        }

        private void OpenProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_settingsService.CurrentSettings.SteamId64))
                Process.Start(new ProcessStartInfo($"https://steamcommunity.com/profiles/{_settingsService.CurrentSettings.SteamId64}") { UseShellExecute = true });
        }

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

                TxtAchievements.Text = game.AchievementString;
                ProgAchievements.Value = game.AchievementPercentage;

                UpdateBoostButtonState();
                OptionsOverlay.Visibility = Visibility.Visible;
                ToggleBlur(true);
            }
        }

        private async void OpenAchievements_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;

            AchievementsOverlay.Visibility = Visibility.Visible;
            TxtAchieveTitle.Text = _selectedGame.Name;
            TxtAchieveSubtitle.Text = "Loading achievements...";
            ListAchievements.ItemsSource = null;

            string key = _settingsService.CurrentSettings.SteamApiKey;
            string id = _settingsService.CurrentSettings.SteamId64;

            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(id))
            {
                var details = await _steamService.GetDetailedAchievements(key, id, _selectedGame.AppId);

                if (details.Count > 0)
                {
                    ListAchievements.ItemsSource = details;
                    TxtAchieveSubtitle.Text = $"{details.Count(x => x.Achieved)} / {details.Count} Unlocked";
                }
                else
                {
                    TxtAchieveSubtitle.Text = "No achievements found or Profile Private.";
                }
            }
            else
            {
                TxtAchieveSubtitle.Text = "Please set API Key & Steam ID in settings.";
            }
        }

        private void Achievement_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SteamAchievement ach)
            {
                Process.Start(new ProcessStartInfo($"https://steamcommunity.com/stats/{_selectedGame.AppId}/achievements") { UseShellExecute = true });
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

            if (!_steamService.IsSteamRunning())
            {
                MessageBox.Show("Steam is not running! Please start Steam before boosting to ensure files are handled safely.", "Safety Check", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
        private void OpenDebug_Click(object sender, RoutedEventArgs e) { DebugOverlay.Visibility = Visibility.Visible; ToggleBlur(true); }

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
                _executor.RestoreIntegrity(_allGames);
                MessageBox.Show("Reset complete.");
            }
        }

        private void DebugOpenConfig_Click(object sender, RoutedEventArgs e) => Process.Start("explorer.exe", Environment.CurrentDirectory);

        private void DebugRestart_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            Application.Current.Shutdown();
        }

        private async void RefreshLibrary_Click(object sender, RoutedEventArgs e) => await ScanLibraryAsync(silent: false);

        private async Task ScanLibraryAsync(bool silent)
        {
            if (!silent) Mouse.OverrideCursor = Cursors.Wait;

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
                    newGame.AchievementsEarned = oldGame.AchievementsEarned;
                    newGame.AchievementsTotal = oldGame.AchievementsTotal;
                }
            }

            string key = _settingsService.CurrentSettings.SteamApiKey;
            string id = _settingsService.CurrentSettings.SteamId64;
            if (!string.IsNullOrEmpty(key) && NetworkInterface.GetIsNetworkAvailable())
            {
                var tasks = new List<Task>
                {
                    _steamService.UpdateHoursFromApi(key, id, newScan),
                    _steamService.UpdateAllAchievementsCount(key, id, newScan)
                };
                await Task.WhenAll(tasks);
            }

            var newlyAdded = newScan.Where(g => !oldGamesDict.ContainsKey(g.AppId)).ToList();
            _allGames = newScan;
            await _steamService.SaveGamesToDiskAsync(_allGames);

            Application.Current.Dispatcher.Invoke(() =>
            {
                ApplyFilter();
                if (!silent) Mouse.OverrideCursor = null;

                if (newlyAdded.Any())
                {
                    NewGamesList.ItemsSource = newlyAdded;
                    NewGamesOverlay.Visibility = Visibility.Visible;
                    ToggleBlur(true);
                }
                else if (!silent)
                {
                    NoNewGamesOverlay.Visibility = Visibility.Visible;
                    ToggleBlur(true);
                }
            });
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.CurrentSettings.SteamApiKey = PbApiKey.Password;
            _settingsService.CurrentSettings.SteamId64 = TxtSteamId.Text;
            _settingsService.SaveSettings();
            await CheckApiStatusSilent();
        }

        private void SavePreferences_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.CurrentSettings.EnableMica = ChkMica.IsChecked == true;
            _settingsService.SaveSettings();
            UpdateBackdrop();
        }

        private async Task CheckApiStatusSilent()
        {
            string key = _settingsService.CurrentSettings.SteamApiKey;
            string id = _settingsService.CurrentSettings.SteamId64;

            if (string.IsNullOrEmpty(key)) return;

            if (!NetworkInterface.GetIsNetworkAvailable()) { UpdateApiStatusUI(false, "Offline (Retrying...)"); return; }
            bool isValid = await _steamService.CheckApiCredentials(key, id);
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

    // --- CONVERTERS ---
    public class AspectRatioConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width && double.Parse(parameter?.ToString() ?? "1", CultureInfo.InvariantCulture) is double ratio)
            {
                return width * ratio;
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}