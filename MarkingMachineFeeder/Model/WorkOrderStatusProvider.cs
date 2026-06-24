using Ewan.Core.Plc;
using Ewan.Model.System;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace MarkingMachineFeeder.Viewmodel
{
    /// <summary>
    /// 后台提供工单绑定状态（A/B/C/D/中段/后段），定期从 SystemParametersManager 读取并通知 UI 更新。
    /// 采用轮询是为了避免修改大量现有逻辑代码；间隔较短（800ms）足以保持 UI 感知实时变化。
    /// </summary>
    public class WorkOrderStatusProvider : INotifyPropertyChanged, IDisposable
    {
        private readonly DispatcherTimer _timer;

        private string _workOrderA = string.Empty;
        private string _workOrderB = string.Empty;
        private string _workOrderC = string.Empty;
        private string _workOrderD = string.Empty;
        private string _midBound = string.Empty;
        private string _rearBound = string.Empty;

        public WorkOrderStatusProvider()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
            Refresh(); // 初始读取
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Refresh();
        }

        public static string Mid;
        public static string Rear;
        bool load=true;
        private void Refresh()
        {
            try
            {
                var parameters = SystemParametersManager.Instance?.Parameters;
                if (parameters == null) return;


                if (load)
                {
                    load = false;
                    ushort length = 10;
                    string primaryAddress = "701";
                    string primaryAddress1 = "711";
                    string secondaryAddress = "731";
                    string secondaryAddress1 = "741";

                    var v_A = ModbusRTUManager.Instance()?.func_Read(primaryAddress, length).Trim('\0');
                    var v_B = ModbusRTUManager.Instance()?.func_Read(primaryAddress1, length).Trim('\0');
                    var v_C = ModbusRTUManager.Instance()?.func_Read(secondaryAddress, length).Trim('\0');
                    var v_D = ModbusRTUManager.Instance()?.func_Read(secondaryAddress1, length).Trim('\0');
                    ModbusRTUManager.Instance().lisWorkOrder[0] = v_A;
                    ModbusRTUManager.Instance().lisWorkOrder[1] = v_B;
                    ModbusRTUManager.Instance().lisWorkOrder[2] = v_C;
                    ModbusRTUManager.Instance().lisWorkOrder[3] = v_D;
                    if (ModbusRTUManager.Instance()?.func_Read("700", 1).Trim('\0') == "\u0001")
                    {
                        ModbusRTUManager.Instance().lisWorkOrder[4] = v_A;
                    }
                    else if (ModbusRTUManager.Instance()?.func_Read("700", 1).Trim('\0') == "\u0002")
                    {
                        ModbusRTUManager.Instance().lisWorkOrder[4] = v_B;
                    }
                    if (ModbusRTUManager.Instance()?.func_Read("730", 1).Trim('\0') == "\u0001")
                    {
                        ModbusRTUManager.Instance().lisWorkOrder[5] = v_C;
                    }
                    else if (ModbusRTUManager.Instance()?.func_Read("730", 1).Trim('\0') == "\u0002")
                    {
                        ModbusRTUManager.Instance().lisWorkOrder[5] = v_D;
                    }
                }


                WorkOrderA = ModbusRTUManager.Instance()?.lisWorkOrder[0];
                WorkOrderB = ModbusRTUManager.Instance()?.lisWorkOrder[1];
                WorkOrderC = ModbusRTUManager.Instance()?.lisWorkOrder[2];
                WorkOrderD = ModbusRTUManager.Instance()?.lisWorkOrder[3];
                MidBound= ModbusRTUManager.Instance()?.lisWorkOrder[4];
                RearBound = ModbusRTUManager.Instance()?.lisWorkOrder[5];
            }
            catch
            {
                // 忽略读取异常，下一次重试
            }
        }

        public string WorkOrderA
        {
            get => _workOrderA;
            private set
            {
                if (_workOrderA == value) return;
                _workOrderA = value;
                RaisePropertyChanged(nameof(WorkOrderA));
            }
        }

        public string WorkOrderB
        {
            get => _workOrderB;
            private set
            {
                if (_workOrderB == value) return;
                _workOrderB = value;
                RaisePropertyChanged(nameof(WorkOrderB));
            }
        }

        public string WorkOrderC
        {
            get => _workOrderC;
            private set
            {
                if (_workOrderC == value) return;
                _workOrderC = value;
                RaisePropertyChanged(nameof(WorkOrderC));
            }
        }

        public string WorkOrderD
        {
            get => _workOrderD;
            private set
            {
                if (_workOrderD == value) return;
                _workOrderD = value;
                RaisePropertyChanged(nameof(WorkOrderD));
            }
        }

        public string MidBound
        {
            get => _midBound;
            private set
            {
                if (_midBound == value) return;
                _midBound = value;
                RaisePropertyChanged(nameof(MidBound));
            }
        }

        public string RearBound
        {
            get => _rearBound;
            private set
            {
                if (_rearBound == value) return;
                _rearBound = value;
                RaisePropertyChanged(nameof(RearBound));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            _timer.Tick -= Timer_Tick;
            _timer.Stop();
        }
    }
}