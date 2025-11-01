using System.Windows;
using System.Windows.Input;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// LoopInteractionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoopInteractionWindow : Window
    {
        public LoopInteractionWindow()
        {
            InitializeComponent();
            DataContext = new MarkingMachineFeeder.Viewmodel.LoopInteractionViewModel();
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
