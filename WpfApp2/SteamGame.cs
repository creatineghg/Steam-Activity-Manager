using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace WpfApp2
{
    // FIX: Implemented INotifyPropertyChanged so percentages update in the UI
    public class SteamAchievement : INotifyPropertyChanged
    {
        public string ApiName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        private bool _achieved;
        public bool Achieved
        {
            get => _achieved;
            set { _achieved = value; OnPropertyChanged(); }
        }

        public string IconUrl { get; set; } = string.Empty;
        public string IconGrayUrl { get; set; } = string.Empty;

        private double _globalPercent;
        public double GlobalPercent
        {
            get => _globalPercent;
            set { _globalPercent = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class SteamGame : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public int AppId { get; set; }
        public string InstallDir { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public bool IsHidden { get; set; } = false;
        public bool IsUtility { get; set; } = false;

        // --- Achievements ---
        private int _achievementsEarned;
        public int AchievementsEarned
        {
            get => _achievementsEarned;
            set { _achievementsEarned = value; OnPropertyChanged(); OnPropertyChanged(nameof(AchievementString)); OnPropertyChanged(nameof(AchievementPercentage)); }
        }

        private int _achievementsTotal;
        public int AchievementsTotal
        {
            get => _achievementsTotal;
            set { _achievementsTotal = value; OnPropertyChanged(); OnPropertyChanged(nameof(AchievementString)); OnPropertyChanged(nameof(AchievementPercentage)); }
        }

        public string AchievementString => AchievementsTotal > 0 ? $"{AchievementsEarned} / {AchievementsTotal}" : "No Data";
        public double AchievementPercentage => AchievementsTotal > 0 ? (double)AchievementsEarned / AchievementsTotal * 100 : 0;

        [JsonIgnore]
        public List<SteamAchievement> AchievementDetails { get; set; } = new List<SteamAchievement>();

        // --- Activity ---
        private bool _isBoosting;
        public bool IsBoosting
        {
            get => _isBoosting;
            set
            {
                if (_isBoosting != value)
                {
                    _isBoosting = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _playtimeMinutes;
        public int PlaytimeMinutes
        {
            get => _playtimeMinutes;
            set
            {
                _playtimeMinutes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PlaytimeFullString));
                OnPropertyChanged(nameof(PlaytimeCompactString));
            }
        }

        public string PlaytimeFullString => PlaytimeMinutes > 0 ? $"{PlaytimeMinutes / 60.0:F1} hours" : "0 hours";

        public string PlaytimeCompactString
        {
            get
            {
                if (PlaytimeMinutes <= 0) return "";
                double hours = PlaytimeMinutes / 60.0;
                if (hours >= 1000) return $"{(hours / 1000.0):F1}k h";
                return $"{hours:F1} h";
            }
        }

        public string ImageUrl => $"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{AppId}/library_600x900.jpg";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}