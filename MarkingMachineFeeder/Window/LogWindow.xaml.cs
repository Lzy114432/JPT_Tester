using MarkingMachineFeeder.Viewmodel;
using System.Windows.Controls;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// LogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LogWindow : UserControl
    {
        public LogWindow()
        {
            InitializeComponent();
            
            // 手动设置ViewModel
            var viewModel = new LogWindowViewModel();
            DataContext = viewModel;
            
            // 订阅滚动事件
            viewModel.ScrollToBottomRequested += () => ScrollToBottom();
            
            // 订阅Unloaded事件进行清理
            this.Unloaded += LogWindow_Unloaded;
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
            if (DataContext is LogWindowViewModel viewModel)
            {
                viewModel.Dispose();
            }
        }
    }
}