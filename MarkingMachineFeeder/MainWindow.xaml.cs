using System.Windows;
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
    }
}
