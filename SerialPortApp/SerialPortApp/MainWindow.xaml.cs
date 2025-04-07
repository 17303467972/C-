using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace SerialPortApp
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Properties
        private SerialPort _serialPort;
        private readonly DispatcherTimer _dataUpdateTimer;
        private PlotModel _plotModel;
        private LineSeries _dataSeries;
        private ManagementEventWatcher _deviceWatcher;
        private DateTimeAxis _dateAxis;
        private LinearAxis _valueAxis;
        private double _alarmThreshold = 80;
        private DateTime _lastAlarmTime = DateTime.MinValue;
        private const double AlarmInterval = 2;

        private ObservableCollection<LogEntry> _logEntries = new ObservableCollection<LogEntry>();
        public ObservableCollection<LogEntry> LogEntries
        {
            get => _logEntries;
            set
            {
                _logEntries = value;
                OnPropertyChanged(nameof(LogEntries));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            
            InitializePlot();
            InitializePortMonitoring();
            RefreshPorts();

            _dataUpdateTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _dataUpdateTimer.Tick += DataUpdateTimer_Tick;

            AddLog("系统初始化完成", LogLevel.Info);
        }

        #region Plot Initialization
        private void InitializePlot()
        {
            _plotModel = new PlotModel
            {
                Title = "实时数据曲线",
                DefaultColors = new List<OxyColor> { OxyColors.RoyalBlue },
                PlotMargins = new OxyThickness(60, 20, 20, 40)
            };

            // 时间轴配置
            _dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm:ss",
                MajorGridlineStyle = LineStyle.Dot,
                MinorGridlineStyle = LineStyle.Dot,
                Title = "时间",
                AbsoluteMinimum = DateTimeAxis.ToDouble(DateTime.MinValue),
                AbsoluteMaximum = DateTimeAxis.ToDouble(DateTime.MaxValue)
            };

            // 数值轴配置
            _valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Dot,
                MinorGridlineStyle = LineStyle.Dot,
                Title = "数值",
                AbsoluteMinimum = double.MinValue,
                AbsoluteMaximum = double.MaxValue
            };

            _plotModel.Axes.Add(_dateAxis);
            _plotModel.Axes.Add(_valueAxis);

            _dataSeries = new LineSeries
            {
                StrokeThickness = 1.5,
                MarkerType = MarkerType.None,
                InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline
            };

            _plotModel.Series.Add(_dataSeries);
            plotView.Model = _plotModel;
        }
        #endregion

        #region Serial Port Management
        private void InitializePortMonitoring()
        {
            try
            {
                var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
                _deviceWatcher = new ManagementEventWatcher(query);
                _deviceWatcher.EventArrived += (s, e) => Dispatcher.Invoke(RefreshPorts);
                _deviceWatcher.Start();
            }
            catch (Exception ex)
            {
                AddLog($"设备监控初始化失败: {ex.Message}", LogLevel.Error);
            }
        }

        private void RefreshPorts()
        {
            try
            {
                var ports = GetDetailedPorts();
                cmbPorts.ItemsSource = ports;
                
                cmbPorts.SelectedIndex = ports.Count > 0 ? 0 : -1;
                AddLog(ports.Count > 0 
                    ? $"检测到{ports.Count}个串口设备" 
                    : "未检测到可用串口", 
                    ports.Count > 0 ? LogLevel.Info : LogLevel.Warning);
            }
            catch (Exception ex)
            {
                AddLog($"刷新端口失败：{ex.Message}", LogLevel.Error);
            }
        }

        private List<PortInfo> GetDetailedPorts()
        {
            var ports = new List<PortInfo>();
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\"");
            
            foreach (var device in searcher.Get())
            {
                var name = device["Caption"]?.ToString();
                if (name?.Contains("(COM") == true)
                {
                    var portName = name.Split(new[] { "(COM" }, StringSplitOptions.None)[1]
                        .TrimEnd(')');
                    
                    ports.Add(new PortInfo
                    {
                        PortName = $"COM{portName}",
                        Description = name.Replace(" (COM", " - COM").Replace(")", "")
                    });
                }
            }
            return ports.OrderBy(p => p.PortName).ToList();
        }
        #endregion

        #region Event Handlers
        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_serialPort?.IsOpen == true) ClosePort();
            else OpenPort();
        }

        private void OpenPort()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var selectedPort = cmbPorts.SelectedItem as PortInfo;
                    if (selectedPort == null) return;

                    if (_serialPort?.IsOpen == true)
                    {
                        AddLog("端口已处于打开状态", LogLevel.Warning);
                        return;
                    }

                    _serialPort = new SerialPort(
                        selectedPort.PortName,
                        int.Parse((cmbBaudRate.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "9600"))
                    {
                        DataBits = 8,
                        Parity = Parity.None,
                        StopBits = StopBits.One,
                        ReadTimeout = 500,
                        WriteTimeout = 500
                    };

                    _serialPort.DataReceived += SerialPort_DataReceived;
                    _serialPort.Open();
            
                    btnOpen.Content = "关闭端口";
                    AddLog($"端口 {selectedPort.PortName} 已打开", LogLevel.Info);
                    _dataUpdateTimer.Start();
                }
                catch (Exception ex)
                {
                    AddLog($"打开端口失败: {ex.Message}", LogLevel.Error);
                    MessageBox.Show(ex.ToString(), "端口错误详情", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private void ClosePort()
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_serialPort == null || !_serialPort.IsOpen) return;

                    _dataUpdateTimer.Stop();
                    _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
            
                    btnOpen.Content = "打开端口";
                    AddLog("端口已关闭", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    AddLog($"关闭端口失败: {ex.Message}", LogLevel.Error);
                }
            });
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (!_serialPort.IsOpen) return;

                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        if (chkHexView.IsChecked == true)
                        {
                            var buffer = new byte[_serialPort.BytesToRead];
                            _serialPort.Read(buffer, 0, buffer.Length);
                            ProcessData(BitConverter.ToString(buffer).Replace("-", " "), true);
                        }
                        else
                        {
                            ProcessData(_serialPort.ReadExisting(), false);
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"数据接收错误: {ex.Message}", LogLevel.Error);
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"数据接收线程错误: {ex.Message}", LogLevel.Error);
            }
        }
        #endregion

        #region Data Processing
        private void ProcessData(string data, bool isHex)
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    txtRawData.AppendText(data + "\n");
                    txtRawData.ScrollToEnd();

                    if (isHex) return;

                    var cleanData = data.Trim()
                        .Replace("\0", "")
                        .Replace(" ", "");

                    var values = new List<double>();

                    // 尝试直接解析单值
                    if (double.TryParse(cleanData, NumberStyles.Any, CultureInfo.InvariantCulture, out var singleValue))
                    {
                        values.Add(singleValue);
                    }
                    else // 尝试解析多值数据
                    {
                        var segments = cleanData.Split(new[] { ',', ';', '\t', '|' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var segment in segments)
                        {
                            var valuePart = segment.Contains(':') 
                                ? segment.Split(new[] { ':' }, 2)[1].Trim() 
                                : segment.Trim();

                            if (double.TryParse(valuePart, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                            {
                                values.Add(val);
                            }
                            else
                            {
                                AddLog($"无效数据段: {segment}", LogLevel.Warning);
                            }
                        }
                    }

                    foreach (var value in values)
                    {
                        UpdateChart(value);
                        CheckAlarm(value);
                    }
                }, DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                AddLog($"数据处理异常: {ex.Message}", LogLevel.Error);
            }
        }

        private void UpdateChart(double value)
        {
            const int maxPoints = 500;
            var now = DateTime.Now;

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var dataPoint = new DataPoint(DateTimeAxis.ToDouble(now), value);
                    _dataSeries.Points.Add(dataPoint);

                    // 限制历史数据量
                    while (_dataSeries.Points.Count > maxPoints)
                        _dataSeries.Points.RemoveAt(0);

                    // 自动调整坐标轴
                    _dateAxis.Minimum = DateTimeAxis.ToDouble(now.AddSeconds(-30));
                    _dateAxis.Maximum = DateTimeAxis.ToDouble(now.AddSeconds(2));

                    var visiblePoints = _dataSeries.Points
                        .Where(p => p.X >= _dateAxis.Minimum && p.X <= _dateAxis.Maximum)
                        .ToList();

                    if (visiblePoints.Any())
                    {
                        double minY = visiblePoints.Min(p => p.Y);
                        double maxY = visiblePoints.Max(p => p.Y);
                        double margin = Math.Max((maxY - minY) * 0.2, 1.0);

                        _valueAxis.Zoom(minY - margin, maxY + margin);
                    }

                    tbChartStatus.Text = $"数据点: {_dataSeries.Points.Count}\n" +
                                       $"最新值: {value:F2}\n" +
                                       $"更新时间: {now:HH:mm:ss.fff}";
                }
                catch (Exception ex)
                {
                    AddLog($"图表更新异常: {ex.Message}", LogLevel.Error);
                }
            }, DispatcherPriority.Render);
        }

        private void CheckAlarm(double value)
        {
            if (value <= _alarmThreshold) return;
            if ((DateTime.Now - _lastAlarmTime).TotalSeconds < AlarmInterval) return;

            _lastAlarmTime = DateTime.Now;
            AddLog($"警报: 当前值 {value:F2} 超过阈值 {_alarmThreshold}", LogLevel.Warning);
            SystemSounds.Beep.Play();
        }
        #endregion

        #region UI Controls
        private void DataUpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                _plotModel.InvalidatePlot(true);
            }
            catch (Exception ex)
            {
                AddLog($"定时刷新异常: {ex.Message}", LogLevel.Error);
            }
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            _dateAxis.Reset();
            _valueAxis.Reset();
            _plotModel.InvalidatePlot(true);
        }

        // private void ExportImage_Click(object sender, RoutedEventArgs e)
        // {
        //     var dialog = new SaveFileDialog
        //     {
        //         Filter = "PNG图片|*.png",
        //         Title = "导出图表"
        //     };
        //
        //     if (dialog.ShowDialog() != true) return;
        //
        //     try
        //     {
        //         new PngExporter { Width = 1200, Height = 800 }
        //             .ExportToFile(_plotModel, dialog.FileName);
        //         AddLog($"图表已导出到: {dialog.FileName}", LogLevel.Info);
        //     }
        //     catch (Exception ex)
        //     {
        //         AddLog($"导出失败: {ex.Message}", LogLevel.Error);
        //     }
        // }

        private void AddLog(string message, LogLevel level)
        {
            Dispatcher.BeginInvoke(() =>
            {
                LogEntries.Insert(0, new LogEntry
                {
                    Timestamp = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Message = message,
                    Level = level.ToString()
                });

                // 限制日志数量
                while (LogEntries.Count > 300)
                    LogEntries.RemoveAt(LogEntries.Count - 1);

                // 自动滚动
                if (dgLogs.Items.Count > 0)
                    dgLogs.ScrollIntoView(dgLogs.Items[0]);
            }, DispatcherPriority.Background);
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                var rand = new Random();
                for (int i = 0; i < 100; i++)
                {
                    var value = 50 + 30 * Math.Sin(i * 0.1) + rand.NextDouble() * 5;
                    UpdateChart(value);
                    Thread.Sleep(50);
                }
            });
        }
        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshPorts();

        private void Window_Closed(object sender, EventArgs e)
        {
            ClosePort();
            _deviceWatcher?.Stop();
            _deviceWatcher?.Dispose();
            _plotModel = null;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
    

    #region Helper Classes
    public class PortInfo
    {
        public string PortName { get; set; }
        public string Description { get; set; }
    }

    public class LogEntry
    {
        public string Timestamp { get; set; }
        public string Message { get; set; }
        public string Level { get; set; }
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public class LogLevelColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "Warning" => Brushes.Orange,
                "Error" => Brushes.Red,
                _ => Brushes.Green
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #endregion
}