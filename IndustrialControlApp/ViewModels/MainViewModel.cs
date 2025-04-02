using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using IndustrialControlApp.Models;
using LiveCharts;
using LiveCharts.Wpf;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IndustrialControlApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DeviceData _deviceData = new();
        private readonly DispatcherTimer _dataUpdateTimer;

        [ObservableProperty]
        private ObservableCollection<string> _alarms = new();

        public SeriesCollection TemperatureSeries { get; } = new();
        public ChartValues<double> TemperatureValues { get; } = new();

        public MainViewModel()
        {
            // 初始化图表
            TemperatureSeries.Add(new LineSeries
            {
                Values = TemperatureValues,
                Title = "温度曲线"
            });

            // 数据更新定时器
            _dataUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _dataUpdateTimer.Tick += UpdateSensorData;
            _dataUpdateTimer.Start();
        }

        private void UpdateSensorData(object? sender, EventArgs e)
        {
            var random = new Random();
            _deviceData.Temperature = random.Next(20, 50);

            // 温度报警检测
            if (_deviceData.Temperature > 45)
            {
                Alarms.Insert(0, $"{DateTime.Now:HH:mm:ss} 温度过高！当前值：{_deviceData.Temperature}℃");
                if (Alarms.Count > 20) Alarms.RemoveAt(Alarms.Count - 1);
            }

            // 更新图表
            TemperatureValues.Add(_deviceData.Temperature);
            if (TemperatureValues.Count > 30) TemperatureValues.RemoveAt(0);
        }

        [RelayCommand]
        private void ToggleMotor()
        {
            _deviceData.MotorStatus = !_deviceData.MotorStatus;
        }

        public DeviceData DeviceData => _deviceData;
    }
}
