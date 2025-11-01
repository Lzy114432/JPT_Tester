using System.Windows;
using System.Windows.Input;
using Prism.Mvvm;
using MarkingMachineFeeder.Viewmodel;

namespace MarkingMachineFeeder
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // 手动设置ViewModel
            DataContext = new MainWindowViewModel();
        }

        /// <summary>
        /// 处理菜单栏的鼠标左键按下事件，实现窗口拖动功能
        /// </summary>
        private void DockPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果鼠标左键被按下，拖动窗口
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 处理主窗体鼠标点击事件，确保主窗体能够获得焦点并置顶
        /// </summary>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            
            // 激活当前窗体并置顶
            if (!this.IsActive)
            {
                this.Activate();
                this.Focus();
            }
        }

        /// <summary>
        /// 处理主窗体激活事件
        /// </summary>
        protected override void OnActivated(System.EventArgs e)
        {
            base.OnActivated(e);
            
            // 确保主窗体真正获得焦点
            this.Focus();
        }

        /// <summary>
        /// 最小化按钮点击事件
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化/还原按钮点击事件
        /// </summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建并显示自定义确认对话框
            var confirmDialog = new MarkingMachineFeeder.Windows.ConfirmationDialog(
                Ewan.Resources.UIStrings.ExitConfirmTitle,
                Ewan.Resources.UIStrings.ExitConfirmMessage,
                false);

            // 如果用户确认退出
            if (confirmDialog.ShowDialog() == true)
            {
                this.Close();
            }
        }
    }
}
