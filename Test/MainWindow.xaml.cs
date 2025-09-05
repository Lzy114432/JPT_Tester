using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ewan.Core.Axis;

namespace Test
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private AxisManager _axisManager;
        private bool _isAxisInitialized = false;
        private System.Windows.Threading.DispatcherTimer _statusUpdateTimer;

        public MainWindow()
        {
            InitializeComponent();
            LogMessage("Application started");
            UpdateAxisStatus();
            InitializeStatusTimer();
        }

        private void InitializeStatusTimer()
        {
            // 创建定时器，每100ms更新一次状态
            _statusUpdateTimer = new System.Windows.Threading.DispatcherTimer();
            _statusUpdateTimer.Interval = TimeSpan.FromMilliseconds(100);
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
        }

        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateAxisStatusDisplay();
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                txtLog.AppendText($"[{timestamp}] {message}\r\n");
                txtLog.ScrollToEnd();
            });
        }

        private void UpdateAxisStatus()
        {
            Dispatcher.Invoke(() =>
            {
                if (_isAxisInitialized)
                {
                    txtAxisStatus.Text = "Initialized";
                    txtAxisStatus.Foreground = Brushes.Green;
                    
                    // 启动状态更新定时器（检查是否为null）
                    if (_statusUpdateTimer != null && !_statusUpdateTimer.IsEnabled)
                        _statusUpdateTimer.Start();
                }
                else
                {
                    txtAxisStatus.Text = "Not Initialized";
                    txtAxisStatus.Foreground = Brushes.Red;
                    
                    // 停止状态更新定时器（检查是否为null）
                    if (_statusUpdateTimer != null && _statusUpdateTimer.IsEnabled)
                        _statusUpdateTimer.Stop();
                    
                    // 清空状态显示
                    txtIsBusy.Text = "N/A";
                    txtIsBusy.Foreground = Brushes.Gray;
                    txtIsAlarm.Text = "N/A";
                    txtIsAlarm.Foreground = Brushes.Gray;
                    txtPosition.Text = "N/A";
                    txtPosition.Foreground = Brushes.Gray;
                }
            });
        }

        private void UpdateAxisStatusDisplay()
        {
            if (!_isAxisInitialized || _axisManager == null)
                return;

            try
            {
                Dispatcher.Invoke(() =>
                {
                    // 更新忙状态
                    bool isBusy = _axisManager.IsBusy;
                    txtIsBusy.Text = isBusy ? "YES" : "NO";
                    txtIsBusy.Foreground = isBusy ? Brushes.Orange : Brushes.Green;
                    
                    // 更新报警状态
                    bool isAlarm = _axisManager.IsAlarm;
                    txtIsAlarm.Text = isAlarm ? "YES" : "NO";
                    txtIsAlarm.Foreground = isAlarm ? Brushes.Red : Brushes.Green;
                    
                    // 更新位置
                    double position = _axisManager.Position;
                    txtPosition.Text = position.ToString("F3");
                    txtPosition.Foreground = Brushes.Blue;
                });
            }
            catch (Exception ex)
            {
                // 避免在状态更新中显示错误，只在控制台输出
                System.Diagnostics.Debug.WriteLine($"Status update error: {ex.Message}");
            }
        }

        // AxisManager 初始化
        private void BtnInitAxis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("Initializing AxisManager...");
                _axisManager = AxisManager.Instance();
                bool result = _axisManager.Init();
                
                if (result)
                {
                    _isAxisInitialized = true;
                    LogMessage("AxisManager initialized successfully");
                }
                else
                {
                    _isAxisInitialized = false;
                    LogMessage("AxisManager initialization failed");
                }
                
                UpdateAxisStatus();
            }
            catch (Exception ex)
            {
                LogMessage($"Error initializing AxisManager: {ex.Message}");
                _isAxisInitialized = false;
                UpdateAxisStatus();
            }
        }

        // AxisManager 销毁
        private void BtnDestroyAxis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_axisManager != null)
                {
                    LogMessage("Destroying AxisManager...");
                    _axisManager.Destroy();
                    LogMessage("AxisManager destroyed");
                }
                else
                {
                    LogMessage("AxisManager is not initialized");
                }
                
                _isAxisInitialized = false;
                UpdateAxisStatus();
            }
            catch (Exception ex)
            {
                LogMessage($"Error destroying AxisManager: {ex.Message}");
            }
        }

        // 正向Jog - 按下开始
        private void BtnJogPositive_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                double speed = double.Parse(txtSpeed.Text);
                double step = double.Parse(txtStep.Text);
                double acc = double.Parse(txtAcc.Text);
                double dec = double.Parse(txtDec.Text);

                _axisManager.DirectJog(speed, step, acc, dec);
                LogMessage($"Started positive jog: Speed={speed}, Step={step}, Acc={acc}, Dec={dec}");
                
                // 改变按钮外观表示正在运行
                btnJogPositive.Background = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in positive jog: {ex.Message}");
            }
        }

        // 正向Jog - 松开停止
        private void BtnJogPositive_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                _axisManager.DirectJogStop();
                LogMessage("Positive jog stopped (mouse up)");
                
                // 恢复按钮外观
                btnJogPositive.ClearValue(Button.BackgroundProperty);
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping positive jog: {ex.Message}");
            }
        }

        // 正向Jog - 鼠标离开时也停止（防止鼠标拖出按钮区域）
        private void BtnJogPositive_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                _axisManager.DirectJogStop();
                LogMessage("Positive jog stopped (mouse leave)");
                
                // 恢复按钮外观
                btnJogPositive.ClearValue(Button.BackgroundProperty);
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping positive jog: {ex.Message}");
            }
        }

        // 负向Jog - 按下开始
        private void BtnJogNegative_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                double speed = -double.Parse(txtSpeed.Text);  // 负向
                double step = double.Parse(txtStep.Text);
                double acc = double.Parse(txtAcc.Text);
                double dec = double.Parse(txtDec.Text);

                _axisManager.DirectJog(speed, step, acc, dec);
                LogMessage($"Started negative jog: Speed={speed}, Step={step}, Acc={acc}, Dec={dec}");
                
                // 改变按钮外观表示正在运行
                btnJogNegative.Background = Brushes.LightCoral;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in negative jog: {ex.Message}");
            }
        }

        // 负向Jog - 松开停止
        private void BtnJogNegative_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                _axisManager.DirectJogStop();
                LogMessage("Negative jog stopped (mouse up)");
                
                // 恢复按钮外观
                btnJogNegative.ClearValue(Button.BackgroundProperty);
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping negative jog: {ex.Message}");
            }
        }

        // 负向Jog - 鼠标离开时也停止
        private void BtnJogNegative_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                _axisManager.DirectJogStop();
                LogMessage("Negative jog stopped (mouse leave)");
                
                // 恢复按钮外观
                btnJogNegative.ClearValue(Button.BackgroundProperty);
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping negative jog: {ex.Message}");
            }
        }

        // 停止Jog
        private void BtnJogStop_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                _axisManager.DirectJogStop();
                LogMessage("Jog stopped");
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping jog: {ex.Message}");
            }
        }

        // 急停
        private void BtnEmgStop_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                _axisManager.EmgStop();
                LogMessage("Emergency stop executed");
            }
            catch (Exception ex)
            {
                LogMessage($"Error in emergency stop: {ex.Message}");
            }
        }

        // 快捷正向Jog（使用默认参数）
        private void BtnQuickJogPos_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                _axisManager.DirectJog(100);  // 使用默认参数
                LogMessage("Quick positive jog started (default parameters)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error in quick positive jog: {ex.Message}");
            }
        }

        // 快捷负向Jog（使用默认参数）
        private void BtnQuickJogNeg_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckAxisInitialized()) return;

            try
            {
                _axisManager.DirectJog(-100);  // 使用默认参数，负向
                LogMessage("Quick negative jog started (default parameters)");
            }
            catch (Exception ex)
            {
                LogMessage($"Error in quick negative jog: {ex.Message}");
            }
        }

        private bool CheckAxisInitialized()
        {
            if (!_isAxisInitialized || _axisManager == null)
            {
                LogMessage("Please initialize AxisManager first!");
                return false;
            }
            return true;
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 停止状态更新定时器
                if (_statusUpdateTimer != null && _statusUpdateTimer.IsEnabled)
                {
                    _statusUpdateTimer.Stop();
                }

                if (_axisManager != null && _isAxisInitialized)
                {
                    _axisManager.Destroy();
                    LogMessage("AxisManager destroyed on application close");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error on close: {ex.Message}");
            }
            
            base.OnClosed(e);
        }
    }
}
