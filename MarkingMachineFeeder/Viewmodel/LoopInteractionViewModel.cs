using Ewan.Core.Logger;
using Prism.Commands;
using Prism.Mvvm;
using System;

namespace MarkingMachineFeeder.Viewmodel
{
    public class LoopInteractionViewModel : BindableBase
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));

        public DelegateCommand ReadRingLineRequestCommand { get; }
        public DelegateCommand WriteUnloadCompleteCommand { get; }

        public LoopInteractionViewModel()
        {
            ReadRingLineRequestCommand = new DelegateCommand(ExecuteReadRingLineRequest);
            WriteUnloadCompleteCommand = new DelegateCommand(ExecuteWriteUnloadComplete);

            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "环线交互窗口初始化");
        }

        /// <summary>
        /// 执行读取环线要料信号命令 - 寄存器152(u16类型)
        /// </summary>
        private void ExecuteReadRingLineRequest()
        {
            try
            {
                var modbusManager = Ewan.Core.Plc.ModbusRTUManager.Instance();

                if (modbusManager == null || !modbusManager.IsConnected())
                {
                    _uiLogger.Warn($"Modbus RTU未连接，无法读取环线要料信号");
                    System.Windows.MessageBox.Show("Modbus RTU未连接", "警告",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 读取寄存器152，u16需要读取2个字节
                byte[] data = modbusManager.Read("152", 2);

                if (data != null && data.Length >= 2)
                {
                    // Modbus大端字节序: 高字节在前，低字节在后
                    ushort value = (ushort)((data[0] << 8) | data[1]);
                    _uiLogger.Info($"环线要料信号读取成功: 值 = {value}");
                    System.Windows.MessageBox.Show($"环线要料信号(寄存器152): {value}", "读取成功",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    _uiLogger.Error("读取环线要料信号失败: 返回数据为空或长度不足");
                    System.Windows.MessageBox.Show("读取失败: 返回数据无效", "错误",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"读取环线要料信号异常: {ex.Message}");
                System.Windows.MessageBox.Show($"读取异常: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行写入下料完成信号命令 - 寄存器153(u16类型)
        /// </summary>
        private void ExecuteWriteUnloadComplete()
        {
            try
            {
                var modbusManager = Ewan.Core.Plc.ModbusRTUManager.Instance();

                if (modbusManager == null || !modbusManager.IsConnected())
                {
                    _uiLogger.Warn($"Modbus RTU未连接，无法写入下料完成信号");
                    System.Windows.MessageBox.Show("Modbus RTU未连接", "警告",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 写入值1到寄存器153 (u16类型)
                ushort valueToWrite = 1;
                var result = modbusManager.WriteAny("153", valueToWrite);

                if (result.IsSuccess)
                {
                    _uiLogger.Info($"写入下料完成信号成功: 值 = {valueToWrite}");
                    System.Windows.MessageBox.Show($"成功写入下料完成信号 {valueToWrite} 到寄存器153", "写入成功",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    _uiLogger.Error($"写入下料完成信号失败: {result.Message}");
                    System.Windows.MessageBox.Show($"写入失败: {result.Message}", "错误",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"写入下料完成信号异常: {ex.Message}");
                System.Windows.MessageBox.Show($"写入异常: {ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
