using MarkingMachineFeeder.Viewmodel;
using System.Windows.Controls;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// LogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LogWindow : UserControl
    {
        private LogWindowViewModel _viewModel;

        public LogWindow()
        {
            InitializeComponent();
            
            // 手动设置ViewModel
            _viewModel = new LogWindowViewModel();
            DataContext = _viewModel;
            
            // 订阅滚动事件
            _viewModel.ScrollToBottomRequested += () => ScrollToBottom();
            
            // 订阅Unloaded事件进行清理
            this.Unloaded += LogWindow_Unloaded;
            
            // 手动设置DataGrid列头，避免绑定问题
            SetColumnHeaders();
            SetContextMenuHeaders();
            
            // 订阅文化变更事件以更新列头
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当UI文本属性变更时，更新列头和菜单
            if (e.PropertyName == nameof(LogWindowViewModel.TimestampHeaderText) ||
                e.PropertyName == nameof(LogWindowViewModel.LevelHeaderText) ||
                e.PropertyName == nameof(LogWindowViewModel.MessageHeaderText))
            {
                SetColumnHeaders();
            }
            else if (e.PropertyName == nameof(LogWindowViewModel.CopySelectedText) ||
                     e.PropertyName == nameof(LogWindowViewModel.CopyAllText) ||
                     e.PropertyName == nameof(LogWindowViewModel.ClearMenuText))
            {
                SetContextMenuHeaders();
            }
        }

        private void SetColumnHeaders()
        {
            if (dgLogs != null && _viewModel != null)
            {
                if (dgLogs.Columns.Count >= 3)
                {
                    dgLogs.Columns[0].Header = _viewModel.TimestampHeaderText ?? "时间";
                    dgLogs.Columns[1].Header = _viewModel.LevelHeaderText ?? "级别";
                    dgLogs.Columns[2].Header = _viewModel.MessageHeaderText ?? "消息";
                }
            }
        }

        private void SetContextMenuHeaders()
        {
            if (dgLogs?.ContextMenu != null && _viewModel != null)
            {
                var menuItems = dgLogs.ContextMenu.Items;
                if (menuItems.Count >= 3)
                {
                    if (menuItems[0] is System.Windows.Controls.MenuItem copySelectedItem)
                        copySelectedItem.Header = _viewModel.CopySelectedText ?? "复制选中行";
                    
                    if (menuItems[1] is System.Windows.Controls.MenuItem copyAllItem)
                        copyAllItem.Header = _viewModel.CopyAllText ?? "复制所有日志";
                    
                    if (menuItems[3] is System.Windows.Controls.MenuItem clearItem) // 跳过Separator
                        clearItem.Header = _viewModel.ClearMenuText ?? "清除日志";
                }
            }
        }

        private void ScrollToBottom()
        {
            if (dgLogs.Items.Count > 0)
            {
                var lastItem = dgLogs.Items[dgLogs.Items.Count - 1];
                dgLogs.ScrollIntoView(lastItem);
            }
        }

        private void LogWindow_Unloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 清理ViewModel资源
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.Dispose();
            }
        }
    }
}