using System.Windows;
using System.Windows.Controls;
using MarkingMachineFeeder.Viewmodel;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// IOMappingConfigWindow.xaml 的交互逻辑
    /// </summary>
    public partial class IOMappingConfigWindow : Window
    {
        public IOMappingConfigWindow()
        {
            InitializeComponent();
            
            // Set the DataContext to the ViewModel
            var viewModel = new IOMappingConfigViewModel();
            DataContext = viewModel;
            
            // Update the window title from ViewModel
            this.Title = viewModel.WindowTitle;
            
            // Manually set column headers since DataGrid columns don't support regular binding
            SetColumnHeaders();
        }
        
        private void SetColumnHeaders()
        {
            // Find the DataGrids and set their column headers
            var inputTab = FindName("InputDataGrid") as DataGrid;
            var outputTab = FindName("OutputDataGrid") as DataGrid;
            
            var vm = DataContext as IOMappingConfigViewModel;
            if (vm != null)
            {
                // Set headers for both input and output DataGrids
                if (inputTab != null && inputTab.Columns.Count >= 4)
                {
                    inputTab.Columns[0].Header = vm.IndexHeaderText;
                    inputTab.Columns[1].Header = vm.NameHeaderText;
                    inputTab.Columns[2].Header = vm.MappingHeaderText;
                    inputTab.Columns[3].Header = vm.StatusHeaderText;
                }
                
                if (outputTab != null && outputTab.Columns.Count >= 4)
                {
                    outputTab.Columns[0].Header = vm.IndexHeaderText;
                    outputTab.Columns[1].Header = vm.NameHeaderText;
                    outputTab.Columns[2].Header = vm.MappingHeaderText;
                    outputTab.Columns[3].Header = vm.StatusHeaderText;
                }
            }
        }
    }
}