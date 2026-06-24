using Ewan.Core.Logic;
using Ewan.Core.Plc;
using Ewan.Model.Messages;
using Ewan.Model.System;
using EwanCommon.Logging;
using EwanCore.Messaging;
using MarkingMachineFeeder.Viewmodel;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
            SystemParametersManager.Instance.Parameters.str_当前环形线工单号 = string.Empty;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("是否确认清除？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
            Button btn = sender as Button;
            if (btn == null)
                return;
            string name = btn.Name;
            switch (name)
            {
                case "清除中段":
                    //如果中段现在绑定的是料仓A，清除料仓A工单，绑定料仓B；如果中段现在绑定的是料仓B，清除料仓B工单，绑定料仓A
                    if (ModbusRTUManager.Instance()?.func_Read("700", 1).Trim('\0') == "\u0001")
                    {
                        _uiLogger.WarnRaw("人工清除中段绑定料仓A");
                        ModbusRTUManager.Instance().lisWorkOrder[0] = "";
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("701", "", 10, "main");

                        if (string.IsNullOrWhiteSpace(ModbusRTUManager.Instance()?.func_Read("711", 10).Trim('\0')))
                        {
                            _uiLogger.WarnRaw("人工:料仓B工单为空，继续绑定料仓A");
                        }
                        else
                        {
                            ModbusRTUManager.Instance().lisWorkOrder[4] = ModbusRTUManager.Instance().lisWorkOrder[1];
                            ModbusRTUManager.Instance()?.WriteAny("700", (ushort)2, "main");
                            SystemParametersManager.Instance.Parameters.i_当前中段环线料仓 = 2;
                        }
                    }
                    else if (ModbusRTUManager.Instance()?.func_Read("700", 1).Trim('\0') == "\u0002")
                    {
                        ModbusRTUManager.Instance().lisWorkOrder[1] = "";
                        ModbusRTUManager.Instance().lisWorkOrder[4] = ModbusRTUManager.Instance().lisWorkOrder[0];
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("711", "", 10, "main");
                        ModbusRTUManager.Instance()?.WriteAny("700", (ushort)1, "main");
                        SystemParametersManager.Instance.Parameters.i_当前中段环线料仓 = 1;
                        _uiLogger.WarnRaw("人工清除中段绑定料仓B");
                    }
                    else
                    {
                        _uiLogger.WarnRaw("人工清除失败");
                    }
                    break;
                case "清除后段":
                    if (ModbusRTUManager.Instance()?.func_Read("730", 1).Trim('\0') == "\u0001")
                    {
                        _uiLogger.WarnRaw("人工清除后段绑定料仓C");
                        ModbusRTUManager.Instance().lisWorkOrder[2] = "";
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("731", "", 10, "main");

                        if (string.IsNullOrWhiteSpace(ModbusRTUManager.Instance()?.func_Read("711", 10).Trim('\0')))
                        {
                            _uiLogger.WarnRaw("人工:料仓D工单为空，继续绑定料仓C");
                        }
                        else
                        {
                            ModbusRTUManager.Instance().lisWorkOrder[5] = ModbusRTUManager.Instance().lisWorkOrder[3];
                            ModbusRTUManager.Instance()?.WriteAny("730", (ushort)2, "main");
                            SystemParametersManager.Instance.Parameters.i_当前后段环线料仓 = 2;
                        }
                    }
                    else if (ModbusRTUManager.Instance()?.func_Read("730", 1).Trim('\0') == "\u0002")
                    {
                        ModbusRTUManager.Instance().lisWorkOrder[3] = "";
                        ModbusRTUManager.Instance().lisWorkOrder[5] = ModbusRTUManager.Instance().lisWorkOrder[2];
                        ModbusRTUManager.Instance()?.WriteStringToRegisters("741", "", 10, "main");
                        ModbusRTUManager.Instance()?.WriteAny("730", (ushort)1, "main");
                        SystemParametersManager.Instance.Parameters.i_当前后段环线料仓 = 1;
                        _uiLogger.WarnRaw("人工清除后段绑定料仓D");
                    }
                    else
                    {
                        _uiLogger.WarnRaw("人工清除失败");
                    }
                    break;
            }


        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            //var a = string.IsNullOrEmpty("");
            //SystemParametersManager.Instance.Parameters.i_料仓1数量++;
            //MessageHub.Current.Post(LoadingUnloadingStateMessage.LoadingCompleted(0, nameof(MaterialLoadingLogic)));
            //MessageHub.Current.Post(LoadingUnloadingStateMessage.UnloadingCompleted(0, nameof(MaterialLoadingLogic)));
            //MessageHub.Current.Post(new PutModeChangedMessage(isPut3Empty1: false));
            //SystemParametersManager.Instance.Parameters.I_持续空车数量 = 3;
            //var a = SystemParametersManager.Instance.Parameters.I_持续空车数量;
            //MessageHub.Current.Post(new Ewan.Model.Messages.PutModeChangedMessage(isPut3Empty1: false));
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
                        break;
                    case "3":
                        SystemParametersManager.Instance.Parameters.i_料仓3数量 = 0;
                        break;
                    case "4":
                        SystemParametersManager.Instance.Parameters.i_料仓NG数量 = 0;
                        break;
                }
                MessageHub.Current.Post(LoadingUnloadingStateMessage.UnloadingCompleted(0, nameof(MaterialLoadingLogic)));
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
        private readonly UILogger _uiLogger = new UILogger();


        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            const ushort length = 10;
            const string primaryAddress = "701";
            const string primaryAddress1 = "711";
            const string secondaryAddress = "731";
            const string secondaryAddress1 = "741";

            var v_A = ModbusRTUManager.Instance()?.func_Read(primaryAddress, length).Trim('\0');
            var v_B = ModbusRTUManager.Instance()?.func_Read(primaryAddress1, length).Trim('\0');
            var v_C = ModbusRTUManager.Instance()?.func_Read(secondaryAddress, length).Trim('\0');
            var v_D = ModbusRTUManager.Instance()?.func_Read(secondaryAddress1, length).Trim('\0');
            string v_中段当前 = "";
            string v_后段当前 = "";
            if (ModbusRTUManager.Instance()?.func_Read("700", 1).Trim('\0') == "\u0001")
            {
                v_中段当前 = "1";
            }
            else if (ModbusRTUManager.Instance()?.func_Read("700", 1).Trim('\0') == "\u0002")
            {
                v_中段当前 = "2";
            }
            if (ModbusRTUManager.Instance()?.func_Read("730", 1).Trim('\0') == "\u0001")
            {
                v_后段当前 = "1";
            }
            else if (ModbusRTUManager.Instance()?.func_Read("730", 1).Trim('\0') == "\u0002")
            {
                v_后段当前 = "2";
            }
            string message = $"/*确定清料仓绑定确认吗？当前中段{v_中段当前},--*/A:{v_A}" +
                $" --B:{v_B}，后段{v_后段当前}, --C:{v_C} --D:{v_D}";
            string caption = "清料仓绑定确认";
            MessageBoxResult result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ModbusRTUManager.Instance()?.WriteStringToRegisters("701", "", 10, "main");
                ModbusRTUManager.Instance()?.WriteStringToRegisters("711", "", 10, "main");
                ModbusRTUManager.Instance()?.WriteStringToRegisters("731", "", 10, "main");
                ModbusRTUManager.Instance()?.WriteStringToRegisters("741", "", 10, "main");
                ModbusRTUManager.Instance()?.WriteAny("700", (ushort)1, "main");
                ModbusRTUManager.Instance()?.WriteAny("730", (ushort)1, "main");
                SystemParametersManager.Instance.Parameters.i_当前中段环线料仓 = 1;
                SystemParametersManager.Instance.Parameters.i_当前后段环线料仓 = 1;

                for (int n = 0; n < ModbusRTUManager.Instance().lisWorkOrder.Count; n++)
                {
                    ModbusRTUManager.Instance().lisWorkOrder[n] = "";
                }
                _uiLogger.WarnRaw("人工清除所有料仓绑定");
            }
        }

        //写入
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null)
                return;
            string name = btn.Name;
            string message = $"确定{name}为当前料仓吗";

            MessageBoxResult result = MessageBox.Show(message, message, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
            {
                return;
            }

            switch (name)
            {
                case "设置A":
                    ModbusRTUManager.Instance()?.WriteAny("700", (ushort)1, "main");
                    SystemParametersManager.Instance.Parameters.i_当前中段环线料仓 = 1;
                    ModbusRTUManager.Instance().lisWorkOrder[4] = ModbusRTUManager.Instance().lisWorkOrder[0];
                    _uiLogger.WarnRaw("人工设置当前料仓A");
                    break;
                case "设置B":
                    ModbusRTUManager.Instance()?.WriteAny("700", (ushort)2, "main");
                    SystemParametersManager.Instance.Parameters.i_当前中段环线料仓 = 2;
                    ModbusRTUManager.Instance().lisWorkOrder[4] = ModbusRTUManager.Instance().lisWorkOrder[1];
                    _uiLogger.WarnRaw("人工设置当前料仓B");
                    break;
                case "设置C":
                    ModbusRTUManager.Instance()?.WriteAny("730", (ushort)1, "main");
                    SystemParametersManager.Instance.Parameters.i_当前后段环线料仓 = 1;
                    ModbusRTUManager.Instance().lisWorkOrder[5] = ModbusRTUManager.Instance().lisWorkOrder[2];
                    _uiLogger.WarnRaw("人工设置当前料仓C");
                    break;
                case "设置D":
                    ModbusRTUManager.Instance()?.WriteAny("730", (ushort)2, "main");
                    SystemParametersManager.Instance.Parameters.i_当前后段环线料仓 = 2;
                    ModbusRTUManager.Instance().lisWorkOrder[5] = ModbusRTUManager.Instance().lisWorkOrder[3];
                    _uiLogger.WarnRaw("人工设置当前料仓D");
                    break;
            }
        }
        //设置
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //if (!SystemParametersManager.Instance.Parameters.dic_料仓单号.Keys.Contains(tb_输入工单号.Text))
            //{
            //    MessageBox.Show("当前AB工单不包含所写入的工单，无法写入");
            //    return;
            //}

            Button btn = sender as Button;
            if (btn == null)
                return;
            string name = btn.Name;
            switch (name)
            {
                case "写入A":
                    ModbusRTUManager.Instance()?.func_手动写入("701", tb_输入工单号A.Text, 10, "main");
                    ModbusRTUManager.Instance().lisWorkOrder[0] = tb_输入工单号A.Text;
                    _uiLogger.WarnRaw($"人工写入料仓A：{tb_输入工单号A.Text}");
                    break;
                case "写入B":
                    ModbusRTUManager.Instance()?.func_手动写入("711", tb_输入工单号B.Text, 10, "main");
                    ModbusRTUManager.Instance().lisWorkOrder[1] = tb_输入工单号B.Text;
                    _uiLogger.WarnRaw($"人工写入料仓B：{tb_输入工单号B.Text}");
                    break;
                case "写入C":
                    ModbusRTUManager.Instance()?.func_手动写入("731", tb_输入工单号C.Text, 10, "main");
                    ModbusRTUManager.Instance().lisWorkOrder[2] = tb_输入工单号C.Text;
                    _uiLogger.WarnRaw($"人工写入料仓C：{tb_输入工单号C.Text}");
                    break;
                case "写入D":
                    ModbusRTUManager.Instance()?.func_手动写入("741", tb_输入工单号D.Text, 10, "main");
                    ModbusRTUManager.Instance().lisWorkOrder[3] = tb_输入工单号D.Text;
                    _uiLogger.WarnRaw($"人工写入料仓D：{tb_输入工单号D.Text}");
                    break;
            }
        }

        
    }
}
