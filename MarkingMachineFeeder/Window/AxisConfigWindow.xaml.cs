using System.Windows;
using MarkingMachineFeeder.Viewmodel;

namespace MarkingMachineFeeder.Windows
{
    /// <summary>
    /// 轴参数配置窗口
    /// </summary>
    public partial class AxisConfigWindow : Window
    {
        public AxisConfigWindow()
        {
            InitializeComponent();
            DataContext = new AxisConfigViewModel();
        }
    }
}