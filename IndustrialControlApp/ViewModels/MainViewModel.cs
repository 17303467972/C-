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
// using IndustrialControlApp.Services;

namespace IndustrialControlApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private const double CoolingRate = 0.5; // 冷却速率℃/秒
        private const double HeatingVariation = 10.0; // 运行时温度波动范围
        
        private readonly DeviceData _deviceData = new();
        private readonly DispatcherTimer _dataUpdateTimer;

        [ObservableProperty]
        private ObservableCollection<string> _alarms = new();

        public SeriesCollection TemperatureSeries { get; } = new();
        public ChartValues<double> TemperatureValues { get; } = new();

        public MainViewModel()
        {
            TemperatureSeries.Add(new LineSeries
            {
                Values = TemperatureValues,
                Title = "温度曲线",
                LineSmoothness = 0.5
            });

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
            
            if (_deviceData.MotorStatus)
            {
                // 运行时温度波动逻辑
                _deviceData.Temperature += (random.NextDouble() - 0.5) * HeatingVariation;
                _deviceData.Temperature = Math.Max(DeviceData.AmbientTemp, _deviceData.Temperature);
            }
            else
            {
                // 停止后冷却逻辑
                if (_deviceData.Temperature > DeviceData.AmbientTemp)
                {
                    var newTemp = _deviceData.Temperature - CoolingRate;
                    _deviceData.Temperature = Math.Max(DeviceData.AmbientTemp, newTemp);
                }
            }

            // 温度报警检测
            if (_deviceData.Temperature > 45)
            {
                Alarms.Insert(0, $"{DateTime.Now:HH:mm:ss} 温度过高！当前值：{_deviceData.Temperature:F1}℃");
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
            
            // 如果从运行转为停止，重置冷却逻辑
            if (!_deviceData.MotorStatus)
            {
                _deviceData.Temperature = Math.Max(_deviceData.Temperature, DeviceData.AmbientTemp);
            }
        }

        public DeviceData DeviceData => _deviceData;
    }
}