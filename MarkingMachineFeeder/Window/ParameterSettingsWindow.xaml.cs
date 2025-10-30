using System.Windows;
using MarkingMachineFeeder.Viewmodel;

namespace MarkingMachineFeeder.Windows
{
    public partial class ParameterSettingsWindow : Window
    {
        public ParameterSettingsWindow()
        {
            InitializeComponent();
            DataContext = new ParameterSettingsViewModel();
        }
    }
}
