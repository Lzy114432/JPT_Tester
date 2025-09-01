using Ewan.Core.Logger;
using Ewan.Core.Msg;
using Ewan.Core.Culture;
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
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
        private readonly CultureManager _cultureManager;
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
            // 初始化CultureManager
            _cultureManager = CultureManager.Instance();
            
            LogEntries = new ObservableCollection<LogEntry>();
            
            ClearCommand = new DelegateCommand(ExecuteClear);
            ExportCommand = new DelegateCommand(ExecuteExport);
            CopySelectedCommand = new DelegateCommand<System.Collections.IList>(ExecuteCopySelected);
            CopyAllCommand = new DelegateCommand(ExecuteCopyAll);

            // 初始化UI文本 - 先初始化文本再订阅事件
            UpdateUITexts();
            
            // 订阅文化变更事件
            _cultureManager.CultureChanged += OnCultureChanged;

            InitializeLogListener();
            AddSampleLogs();
        }

        private void OnCultureChanged(object sender, CultureChangedEventArgs e)
        {
            // 同步资源文件的Culture设置
            Ewan.Resources.LogStrings.Culture = e.NewCulture;
            UpdateUITexts();
        }

        private void UpdateUITexts()
        {
            try
            {
                // 确保资源文件使用正确的语言
                Ewan.Resources.LogStrings.Culture = _cultureManager?.CurrentCulture;
                
                ClearButtonText = Ewan.Resources.LogStrings.ClearLogs ?? "Clear Logs";
                ExportButtonText = Ewan.Resources.LogStrings.ExportLogs ?? "Export Logs";
                AutoScrollText = Ewan.Resources.LogStrings.AutoScroll ?? "Auto Scroll";
                MaxLinesText = Ewan.Resources.LogStrings.MaxLines ?? "Max Lines:";
                TimestampHeaderText = Ewan.Resources.LogStrings.Timestamp ?? "Time";
                LevelHeaderText = Ewan.Resources.LogStrings.Level ?? "Level";
                MessageHeaderText = Ewan.Resources.LogStrings.Message ?? "Message";
                CopySelectedText = Ewan.Resources.LogStrings.CopySelected ?? "Copy Selected";
                CopyAllText = Ewan.Resources.LogStrings.CopyAllLogs ?? "Copy All Logs";
                ClearMenuText = Ewan.Resources.LogStrings.ClearMenuText ?? "Clear Logs";
            }
            catch (Exception ex)
            {
                // 如果资源加载失败，使用英文默认值
                System.Diagnostics.Debug.WriteLine($"LogWindow UpdateUITexts error: {ex.Message}");
                ClearButtonText = "Clear Logs";
                ExportButtonText = "Export Logs";
                AutoScrollText = "Auto Scroll";
                MaxLinesText = "Max Lines:";
                TimestampHeaderText = "Time";
                LevelHeaderText = "Level";
                MessageHeaderText = "Message";
                CopySelectedText = "Copy Selected";
                CopyAllText = "Copy All Logs";
                ClearMenuText = "Clear Logs";
            }
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
                    Filter = Ewan.Resources.LogStrings.LogExportFilter,
                    DefaultExt = "txt",
                    FileName = string.Format(Ewan.Resources.LogStrings.LogExportFileName, DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Format(Ewan.Resources.LogStrings.LogExportHeader, 
                        TimestampHeaderText, LevelHeaderText, MessageHeaderText));

                    foreach (var entry in LogEntries)
                    {
                        sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{entry.Level}\t{entry.Message}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show(
                        string.Format(Ewan.Resources.LogStrings.ExportSuccessMessage, saveFileDialog.FileName), 
                        Ewan.Resources.LogStrings.ExportSuccess, 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Ewan.Resources.LogStrings.ExportFailedMessage, ex.Message), 
                    Ewan.Resources.LogStrings.Error, 
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
                    string.Format(Ewan.Resources.LogStrings.CopyFailedMessage, ex.Message), 
                    Ewan.Resources.LogStrings.Error, 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
            }
        }

        private void ExecuteCopyAll()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Format(Ewan.Resources.LogStrings.LogExportHeader, 
                    TimestampHeaderText, LevelHeaderText, MessageHeaderText));

                foreach (var entry in LogEntries)
                {
                    sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{entry.Level}\t{entry.Message}");
                }

                Clipboard.SetText(sb.ToString());
                MessageBox.Show(
                    string.Format(Ewan.Resources.LogStrings.CopySuccessMessage, LogEntries.Count), 
                    Ewan.Resources.LogStrings.CopySuccess, 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(Ewan.Resources.LogStrings.CopyFailedMessage, ex.Message), 
                    Ewan.Resources.LogStrings.Error, 
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
                
                if (_cultureManager != null)
                {
                    _cultureManager.CultureChanged -= OnCultureChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogWindowViewModel dispose error: {ex.Message}");
            }
        }
    }
}