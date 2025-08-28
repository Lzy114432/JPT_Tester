using System.Windows;
using System.Windows.Input;
using Prism.Mvvm;
using MarkingMachineFeeder.Viewmodel;

namespace MarkingMachineFeeder
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
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
    }
}
