using MarkingMachineFeeder.Viewmodel;
using System.Windows;
using System.Windows.Input;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// LoginWindow.xaml 的交互逻辑
    /// </summary>
    public partial class LoginWindow : System.Windows.Window
    {
        private LoginWindowViewModel _viewModel;

        public LoginWindow()
        {
            InitializeComponent();
            _viewModel = new LoginWindowViewModel();
            DataContext = _viewModel;
            
            // 订阅登录成功事件
            _viewModel.LoginSuccessful += OnLoginSuccessful;
            
            // 设置焦点到密码输入框
            Loaded += (s, e) => PasswordBox.Focus();
            
            // 支持鼠标拖拽移动窗口
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        }

        private void OnLoginSuccessful(object sender, System.EventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.LoginSuccessful -= OnLoginSuccessful;
            }
            base.OnClosed(e);
        }
    }
}