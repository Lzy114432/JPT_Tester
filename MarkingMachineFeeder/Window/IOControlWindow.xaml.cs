using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MarkingMachineFeeder.Viewmodel;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// IOControlWindow.xaml 的交互逻辑
    /// </summary>
    public partial class IOControlWindow : System.Windows.Window
    {
        private IOControlViewModel _viewModel;

        public IOControlWindow()
        {
            InitializeComponent();
            _viewModel = new IOControlViewModel();
            DataContext = _viewModel;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 允许拖动窗口
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _viewModel?.Cleanup();
        }

        private void MappingButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SimulateInputButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}