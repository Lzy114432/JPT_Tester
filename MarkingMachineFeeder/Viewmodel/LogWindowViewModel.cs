using Ewan.Core.Logger;
using Ewan.Core.Msg;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using UILogMsg = Ewan.Model.Messages.UILogMsg;
using UILogLevel = Ewan.Model.Messages.LogLevel;

namespace MarkingMachineFeeder.Viewmodel
{
    /// <summary>
    /// 日志显示条目
    /// </summary>
    public class LogEntry : INotifyPropertyChanged
    {
        private DateTime _timestamp;
        private UILogLevel _level;
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

        public UILogLevel Level
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
        private readonly UILogger _uiLogger = new UILogger();
        private MsgListener _logListener;
        private ObservableCollection<LogEntry> _logEntries;
        private bool _autoScroll = true;
        private string _maxLines = "1000";

        // UI文本属性
        private string _clearButtonText;
        private string _exportButtonText;
        private string _autoScrollText;
        private string _maxLinesText;
        private string _timestampHeaderText;
        private string _levelHeaderText;
        private string _messageHeaderText;
        private string _copySelectedText;
        private string _copyAllText;
        private string _clearMenuText;

        #region Properties

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

        public string ClearButtonText
        {
            get => _clearButtonText;
            set => SetProperty(ref _clearButtonText, value);
        }

        public string ExportButtonText
        {
            get => _exportButtonText;
            set => SetProperty(ref _exportButtonText, value);
        }

        public string AutoScrollText
        {
            get => _autoScrollText;
            set => SetProperty(ref _autoScrollText, value);
        }

        public string MaxLinesText
        {
            get => _maxLinesText;
            set => SetProperty(ref _maxLinesText, value);
        }

        public string TimestampHeaderText
        {
            get => _timestampHeaderText;
            set => SetProperty(ref _timestampHeaderText, value);
        }

        public string LevelHeaderText
        {
            get => _levelHeaderText;
            set => SetProperty(ref _levelHeaderText, value);
        }

        public string MessageHeaderText
        {
            get => _messageHeaderText;
            set => SetProperty(ref _messageHeaderText, value);
        }

        public string CopySelectedText
        {
            get => _copySelectedText;
            set => SetProperty(ref _copySelectedText, value);
        }

        public string CopyAllText
        {
            get => _copyAllText;
            set => SetProperty(ref _copyAllText, value);
        }

        public string ClearMenuText
        {
            get => _clearMenuText;
            set => SetProperty(ref _clearMenuText, value);
        }

        #endregion

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

            // 初始化UI文本 - 先初始化文本再订阅事件
            UpdateUITexts();

            InitializeLogListener();
            AddSampleLogs();
        }

        private void UpdateUITexts()
        {
            ClearButtonText = "清除日志";
            ExportButtonText = "导出日志";
            AutoScrollText = "自动滚动";
            MaxLinesText = "最大行数:";
            TimestampHeaderText = "时间";
            LevelHeaderText = "级别";
            MessageHeaderText = "消息";
            CopySelectedText = "复制选中行";
            CopyAllText = "复制所有日志";
            ClearMenuText = "清除日志";
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
                var message = !string.IsNullOrEmpty(logMsg.RawMessage)
                    ? logMsg.RawMessage
                    : FormatMessage(logMsg.MessageKey, logMsg.Parameters);

                var logEntry = new LogEntry
                {
                    Timestamp = logMsg.Timestamp,
                    Level = logMsg.Level,
                    Message = message
                };

                // 在UI线程上添加日志条目
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.BeginInvoke(new Action(() =>
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogWindowViewModel error: {ex.Message}");
            }
        }

        private static string FormatMessage(string template, object[] parameters)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            if (parameters == null || parameters.Length == 0)
            {
                return template;
            }

            try
            {
                return string.Format(template, parameters);
            }
            catch (FormatException)
            {
                return template + " " + string.Join(", ", parameters);
            }
        }

        private void AddSampleLogs()
        {
            // 使用UILogger记录示例日志，实现UI显示和文件保存双重输出
            _uiLogger.Info("系统初始化成功");
            System.Threading.Thread.Sleep(100); // 确保时间戳不同
            _uiLogger.Info("系统状态: 正常");
            System.Threading.Thread.Sleep(100);
            _uiLogger.Warn("Log4net配置文件未找到");
            System.Threading.Thread.Sleep(100);
            _uiLogger.Info("数据库连接成功");
            System.Threading.Thread.Sleep(100);
            _uiLogger.Info("ViewModelLocator已配置");
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
                    FileName = string.Format("日志导出_{0}", DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Format("{0}\t{1}\t{2}", TimestampHeaderText, LevelHeaderText, MessageHeaderText));

                    foreach (var entry in LogEntries)
                    {
                        sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{entry.Level}\t{entry.Message}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show(
                        string.Format("日志已导出到: {0}", saveFileDialog.FileName), 
                        "导出成功", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("导出日志失败: {0}", ex.Message), 
                    "错误", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
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
                MessageBox.Show(
                    string.Format("复制失败: {0}", ex.Message), 
                    "错误", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
            }
        }

        private void ExecuteCopyAll()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Format("{0}\t{1}\t{2}", TimestampHeaderText, LevelHeaderText, MessageHeaderText));

                foreach (var entry in LogEntries)
                {
                    sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{entry.Level}\t{entry.Message}");
                }

                Clipboard.SetText(sb.ToString());
                MessageBox.Show(
                    string.Format("已复制 {0} 条日志到剪贴板", LogEntries.Count), 
                    "复制成功", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format("复制失败: {0}", ex.Message), 
                    "错误", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_logListener != null)
                {
                    MsgManager.Instance().UnRegisterListener(_logListener);
                }
                
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogWindowViewModel dispose error: {ex.Message}");
            }
        }
    }
}
