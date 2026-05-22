using Ewan.Core.Plc;
using Ewan.Model.System;
using MarkingMachineFeeder.Viewmodel;
using Prism.Mvvm;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MarkingMachineFeeder
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // 手动设置ViewModel
            DataContext = new MainWindowViewModel();
        }

        /// <summary>
        /// 处理菜单栏的鼠标左键按下事件，实现窗口拖动功能
        /// </summary>
        private void DockPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 如果鼠标左键被按下，拖动窗口
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// 处理主窗体鼠标点击事件，确保主窗体能够获得焦点并置顶
        /// </summary>
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            // 激活当前窗体并置顶
            if (!this.IsActive)
            {
                this.Activate();
                this.Focus();
            }
        }

        /// <summary>
        /// 处理主窗体激活事件
        /// </summary>
        protected override void OnActivated(System.EventArgs e)
        {
            base.OnActivated(e);

            // 确保主窗体真正获得焦点
            this.Focus();
        }

        /// <summary>
        /// 最小化按钮点击事件
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化/还原按钮点击事件
        /// </summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建并显示自定义确认对话框
            var confirmDialog = new MarkingMachineFeeder.Windows.ConfirmationDialog(
                "退出确认",
                "确定要退出应用程序吗？",
                false);

            // 如果用户确认退出
            if (confirmDialog.ShowDialog() == true)
            {
                var manager = SystemParametersManager.Instance;
                var parameters = manager.Parameters;

                //var parameters = Ewan.Model.System.SystemParameters.CreateDefault();
                manager.SaveParameters(parameters);
                Application.Current.Shutdown();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SystemParametersManager.Instance.Parameters.dic_料仓单号.Clear();
            SystemParametersManager.Instance.Parameters.str_当前工单号 = string.Empty;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // 获取 ComboBox 中选中的值（Tag）
            if (cmbStation.SelectedItem is ComboBoxItem selectedItem)
            {
                string stationTag = selectedItem.Tag.ToString(); // "A", "B", "C", "D"
                switch (stationTag)
                {
                    case "A":
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("701", "", 10, "mian");
                        break;
                    case "B":
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("711", "", 10, "mian");
                        // 清除 B 料仓工单号
                        break;
                    case "C":
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("731", "", 10, "mian");
                        // 清除 C 料仓工单号
                        break;
                    case "D":
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("741", "", 10, "mian");
                        // 清除 D 料仓工单号
                        break;
                }
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (cmbNgBin.SelectedItem is ComboBoxItem selectedItem)
            {
                string ngTag = selectedItem.Tag.ToString(); // "1", "2", "3"
                switch (ngTag)
                {
                    case "1":
                        SystemParametersManager.Instance.Parameters.i_料仓1数量 = 0;
                        break;
                    case "2":
                        SystemParametersManager.Instance.Parameters.i_料仓2数量 = 0;
                        // 清除 2 号 NG 料仓的数量
                        break;
                    case "3":
                        SystemParametersManager.Instance.Parameters.i_料仓3数量 = 0;
                        // 清除 3 号 NG 料仓的数量
                        break;
                    case "NG":
                        SystemParametersManager.Instance.Parameters.i_料仓NG数量 = 0;
                        // 清除 3 号 NG 料仓的数量
                        break;
                }
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SystemParametersManager.Instance.Parameters.b_启用释放空车 = true;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SystemParametersManager.Instance.Parameters.b_启用释放空车 = false;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (!SystemParametersManager.Instance.Parameters.dic_料仓单号.Keys.Contains(tb_输入工单号.Text))
            {
                MessageBox.Show("当前AB工单不包含所写入的工单，无法写入");
                return;
            }
            // 获取 ComboBox 中选中的值（Tag）
            if (cmbStation.SelectedItem is ComboBoxItem selectedItem)
            {
                string stationTag = selectedItem.Tag.ToString(); // "A", "B", "C", "D"
                switch (stationTag)
                {
                    case "A":
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("701", tb_输入工单号.Text, 10, "mian");
                        break;
                    case "B":
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("711", tb_输入工单号.Text, 10, "mian");
                        // 清除 B 料仓工单号
                        break;
                    case "C":
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("731", tb_输入工单号.Text, 10, "mian");
                        // 清除 C 料仓工单号
                        break;
                    case "D":
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("741", tb_输入工单号.Text, 10, "mian");
                        // 清除 D 料仓工单号
                        break;
                }
            }
        }
    }
}
