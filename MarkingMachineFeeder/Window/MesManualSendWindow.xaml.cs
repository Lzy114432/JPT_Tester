using MarkingMachineFeeder.Viewmodel;
using System;
using System.Windows;
using System.Windows.Input;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// MesManualSendWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MesManualSendWindow : Window
    {
        public MesManualSendWindow()
        {
            InitializeComponent();
            DataContext = new MesManualSendViewModel();
            Closed += MesManualSendWindow_Closed;
        }

        private void MesManualSendWindow_Closed(object sender, EventArgs e)
        {
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

