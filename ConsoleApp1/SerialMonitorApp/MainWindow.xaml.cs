using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;



using System.Windows;
using System.Windows.Threading;

namespace SerialMonitorApp
{
    public partial class MainWindow : Window
    {
        private bool _isConnected = false;
        private DispatcherTimer _dataTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTimer();
        }

        private void InitializeTimer()
        {
            _dataTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _dataTimer.Tick += DataTimer_Tick;
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            _isConnected = !_isConnected;
            
            BtnConnect.Content = _isConnected ? "Disconnect" : "Connect";
            TxtStatus.Text = _isConnected ? "Connected" : "Disconnected";
            
            if (_isConnected) _dataTimer.Start();
            else _dataTimer.Stop();
        }

        private void DataTimer_Tick(object sender, EventArgs e)
        {
            // 模拟接收数据
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var data = $"[{timestamp}] Received: {new Random().Next(0, 100)}\n";
            
            // 更新UI（确保线程安全）
            Dispatcher.Invoke(() => 
            {
                TxtMonitor.AppendText(data);
                TxtMonitor.ScrollToEnd();
            });
        }
    }
}