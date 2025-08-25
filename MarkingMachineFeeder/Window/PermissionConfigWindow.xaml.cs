using MarkingMachineFeeder.Viewmodel;
using System.Windows;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// PermissionConfigWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PermissionConfigWindow : Window
    {
        private PermissionConfigViewModel _viewModel;

        public PermissionConfigWindow()
        {
            InitializeComponent();
            _viewModel = new PermissionConfigViewModel();
            DataContext = _viewModel;
            
            // 订阅关闭事件
            _viewModel.CloseRequested += OnCloseRequested;
        }

        private void OnCloseRequested(object sender, System.EventArgs e)
        {
            DialogResult = _viewModel.DialogResult;
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CloseRequested -= OnCloseRequested;
            }
            base.OnClosed(e);
        }
    }
}