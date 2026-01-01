using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp2
{
    public class SteamGame : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public int AppId { get; set; }
        public string InstallDir { get; set; } = string.Empty;

        // Stores the specific EXE path (e.g. C:\Games\GTA5\GTA5.exe)
        public string ExecutablePath { get; set; } = string.Empty;

        public bool IsHidden { get; set; } = false;
        public bool IsUtility { get; set; } = false;

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