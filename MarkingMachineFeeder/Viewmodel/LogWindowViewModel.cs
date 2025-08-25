using Ewan.Core.Logger;
using Ewan.Core.Msg;
using Ewan.Model.Messages;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;

namespace MarkingMachineFeeder.Viewmodel
{
    /// <summary>
    /// 日志显示条目
    /// </summary>
    public class LogEntry : INotifyPropertyChanged
    {
        private DateTime _timestamp;
        private LogLevel _level;
        private string _message;

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }

        public LogLevel Level
        {
            get => _level;
            set
            {
                _level = value;
                OnPropertyChanged(nameof(Level));
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                OnPropertyChanged(nameof(Message));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LogWindowViewModel : BindableBase, IDisposable
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private MsgListener _logListener;
        private ObservableCollection<LogEntry> _logEntries;
        private bool _autoScroll = true;
        private string _maxLines = "1000";

        public ObservableCollection<LogEntry> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }

        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        public string MaxLines
        {
            get => _maxLines;
            set => SetProperty(ref _maxLines, value);
        }

        public DelegateCommand ClearCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand<System.Collections.IList> CopySelectedCommand { get; }
        public DelegateCommand CopyAllCommand { get; }

        public event Action ScrollToBottomRequested;

        public LogWindowViewModel()
        {
            LogEntries = new ObservableCollection<LogEntry>();
            
            ClearCommand = new DelegateCommand(ExecuteClear);
            ExportCommand = new DelegateCommand(ExecuteExport);
            CopySelectedCommand = new DelegateCommand<System.Collections.IList>(ExecuteCopySelected);
            CopyAllCommand = new DelegateCommand(ExecuteCopyAll);

            InitializeLogListener();
            AddSampleLogs();
        }

        private void InitializeLogListener()
        {
            _logListener = new MsgListener(MsgSubject.UILog, OnLogMessageReceived);
            MsgManager.Instance().RegisterListener(_logListener);
        }

        private void OnLogMessageReceived(MessageModel msg)
        {
            try
            {
                var logMsg = msg.GetData<UILogMsg>();
                var message = string.IsNullOrEmpty(logMsg.RawMessage)
                    ? _uiLogger.GetLocalizedMessage(logMsg.MessageKey, logMsg.Parameters)
                    : logMsg.RawMessage;

                var logEntry = new LogEntry
                {
                    Timestamp = logMsg.Timestamp,
                    Level = logMsg.Level,
                    Message = message
                };

                // 在UI线程上添加日志条目
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogEntries.Add(logEntry);

                    // 限制最大行数
                    if (int.TryParse(MaxLines, out int maxLines))
                    {
                        while (LogEntries.Count > maxLines)
                        {
                            LogEntries.RemoveAt(0);
                        }
                    }

                    // 如果启用自动滚动，触发滚动到底部事件
                    if (AutoScroll)
                    {
                        ScrollToBottomRequested?.Invoke();
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogWindowViewModel error: {ex.Message}");
            }
        }

        private void AddSampleLogs()
        {
            // 使用UILogger记录示例日志，实现UI显示和文件保存双重输出
            _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemInitialized);
            System.Threading.Thread.Sleep(100); // 确保时间戳不同
            _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemStatusNormal);
            System.Threading.Thread.Sleep(100);
            _uiLogger.Warn(() => Ewan.Resources.LogMessages.Log4netConfigNotFound);
            System.Threading.Thread.Sleep(100);
            _uiLogger.Info(() => Ewan.Resources.LogMessages.DatabaseConnected);
            System.Threading.Thread.Sleep(100);
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ViewModelLocatorConfigured);
        }

        private void ExecuteClear()
        {
            LogEntries.Clear();
        }

        private void ExecuteExport()
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "文本文件 (*.txt)|*.txt|CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"日志导出_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("时间\t级别\t消息");

                    foreach (var entry in LogEntries)
                    {
                        sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{entry.Level}\t{entry.Message}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"日志已导出到: {saveFileDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteCopySelected(System.Collections.IList selectedItems)
        {
            try
            {
                if (selectedItems != null && selectedItems.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (LogEntry item in selectedItems)
                    {
                        sb.AppendLine($"{item.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{item.Level}\t{item.Message}");
                    }
                    Clipboard.SetText(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExecuteCopyAll()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("时间\t级别\t消息");

                foreach (var entry in LogEntries)
                {
                    sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{entry.Level}\t{entry.Message}");
                }

                Clipboard.SetText(sb.ToString());
                MessageBox.Show($"已复制 {LogEntries.Count} 条日志到剪贴板", "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void Dispose()
        {
            if (_logListener != null)
            {
                MsgManager.Instance().UnRegisterListener(_logListener);
            }
        }
    }
}