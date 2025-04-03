using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace IndustrialControlApp.Models
{
    public class DeviceData : INotifyPropertyChanged
    {
        public const double AmbientTemp = 25.0;
        
        private bool _motorStatus;
        private double _temperature = AmbientTemp;

        public bool MotorStatus
        {
            get => _motorStatus;
            set
            {
                if (_motorStatus == value) return;
                _motorStatus = value;
                OnPropertyChanged();
            }
        }

        public double Temperature
        {
            get => _temperature;
            set
            {
                if (Math.Abs(_temperature - value) < 0.01) return;
                _temperature = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}