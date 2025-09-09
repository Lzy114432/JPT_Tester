using System.Windows;
using MarkingMachineFeeder.Viewmodel;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// AxisControlWindow.xaml 的交互逻辑
    /// </summary>
    public partial class AxisControlWindow : Window
    {
        public AxisControlWindow()
        {
            InitializeComponent();
            DataContext = new AxisControlViewModel();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            // 清理资源
            if (DataContext is AxisControlViewModel viewModel)
            {
                viewModel.Dispose();
            }
            base.OnClosed(e);
        }
    }
}