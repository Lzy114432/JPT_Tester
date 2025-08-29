using System.Windows;
using System.Windows.Input;
using Prism.Mvvm;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// ConfirmationDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialogViewModel ViewModel { get; }

        /// <summary>
        /// 创建一个确认对话框
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="message">确认消息</param>
        /// <param name="showCancelButton">是否显示"取消"按钮</param>
        public ConfirmationDialog(string title, string message, bool showCancelButton = false)
        {
            InitializeComponent();

            // 创建ViewModel
            ViewModel = new ConfirmationDialogViewModel
            {
                Title = title,
                Message = message,
                ShowCancelButton = showCancelButton
            };

            // 设置DataContext
            DataContext = ViewModel;
            
            // 确保窗口显示在主窗口的前面
            Owner = Application.Current.MainWindow;
        }

        /// <summary>
        /// 处理标题栏的鼠标左键按下事件，实现窗口拖动功能
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 处理关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 处理"是"按钮点击事件
        /// </summary>
        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 处理"否"按钮点击事件
        /// </summary>
        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 处理"取消"按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = null;
            Close();
        }
    }

    /// <summary>
    /// 确认对话框ViewModel
    /// </summary>
    public class ConfirmationDialogViewModel : BindableBase
    {
        private string _title;
        private string _message;
        private bool _showCancelButton;

        /// <summary>
        /// 对话框标题
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 确认消息
        /// </summary>
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        /// <summary>
        /// 是否显示"取消"按钮
        /// </summary>
        public bool ShowCancelButton
        {
            get => _showCancelButton;
            set
            {
                SetProperty(ref _showCancelButton, value);
                RaisePropertyChanged(nameof(CancelButtonVisibility));
            }
        }

        /// <summary>
        /// "是"按钮文本
        /// </summary>
        public string YesButtonText => Ewan.Resources.UIStrings.ButtonYes;

        /// <summary>
        /// "否"按钮文本
        /// </summary>
        public string NoButtonText => Ewan.Resources.UIStrings.ButtonNo;

        /// <summary>
        /// "取消"按钮文本
        /// </summary>
        public string CancelButtonText => Ewan.Resources.UIStrings.ButtonCancel;

        /// <summary>
        /// "取消"按钮可见性
        /// </summary>
        public Visibility CancelButtonVisibility => ShowCancelButton ? Visibility.Visible : Visibility.Collapsed;
    }
}