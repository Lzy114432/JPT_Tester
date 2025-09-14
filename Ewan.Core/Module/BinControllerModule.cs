using System;
using System.Threading;
using Ewan.Model.Production;
using Ewan.Model.System;
using Ewan.Core.IO;
using Ewan.Core.Msg;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 料仓控制器模块
    /// 监听IO信号，自动发送料仓控制指令
    /// </summary>
    public class BinControllerModule : BaseModule<BinControllerModule>
    {
        #region 私有字段

        private int _scanInterval = 50; // 扫描间隔，毫秒
        private readonly object _stateLock = new object();
        
        // 系统状态
        private bool _systemStarted = false;
        private SystemMode _currentMode = SystemMode.Manual;

        // IO管理器和消息队列
        private LayeredIOManager _ioManager;
        private MsgManager _msgManager;
        private MsgListener _systemStatusListener;
        
        // IO信号状态缓存（用于边沿检测）
        private bool _lastRobotPickupCompletedSignal = false;    // IN10: 机械臂取料完成信号
        private bool _lastRobotPlaceCompletedSignal = false;     // IN8:  机械臂放置完成信号
        
        // 料仓选择逻辑（轮询方式）
        private int _currentBinIndex = 1; // 当前选择的料仓（1, 2, 3）
        
        #endregion

        #region BaseModule 实现

        protected override void OnInit()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "BinControllerModule");
                
                // 初始化IO管理器和消息队列
                _ioManager = LayeredIOManager.Instance();
                _msgManager = MsgManager.Instance();
                
                // 注册系统状态消息监听
                _systemStatusListener = new MsgListener(MsgSubject.SystemStatus, OnSystemStatusChanged);
                _msgManager.RegisterListener(_systemStatusListener);
                
                // 初始化IO信号状态
                InitializeIOStates();
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.InitializationCompleted, "料仓控制器模块");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "BinControllerModule", ex.Message);
                throw;
            }
        }

        protected override bool OnRun()
        {
            try
            {
                // 检查是否满足运行条件
                if (!ShouldRunController())
                {
                    Thread.Sleep(_scanInterval);
                    return true;
                }

                lock (_stateLock)
                {
                    // 监听机械臂信号并处理
                    ProcessRobotSignals();
                }
                
                Thread.Sleep(_scanInterval);
                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "BinControllerModule", ex.Message);
                Thread.Sleep(1000); // 错误时等待更长时间
                return true;
            }
        }

        protected override void OnDestroy()
        {
            try
            {
                // 注销消息监听器
                if (_systemStatusListener != null)
                {
                    _msgManager.UnregisterListener(_systemStatusListener);
                }
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "BinControllerModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "BinControllerModule销毁", ex.Message);
            }
        }

        #endregion

        #region 核心控制逻辑

        /// <summary>
        /// 检查是否应该运行控制器
        /// </summary>
        /// <returns>是否应该运行</returns>
        private bool ShouldRunController()
        {
            // 在系统启动且为自动模式下运行
            return _systemStarted && _currentMode == SystemMode.Auto;
        }

        /// <summary>
        /// 处理机械臂信号
        /// </summary>
        private void ProcessRobotSignals()
        {
            try
            {
                // 读取当前IO信号状态
                bool currentPickupSignal = ReadIOSignal(10);     // IN10: 机械臂取料完成信号
                bool currentPlaceSignal = ReadIOSignal(8);       // IN8:  机械臂放置完成信号
                
                // 检测机械臂取料完成信号的上升沿（false → true）
                if (!_lastRobotPickupCompletedSignal && currentPickupSignal)
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        "检测到机械臂取料完成信号，发送料仓上料位置指令");
                    
                    // 发送料仓上料位置指令
                    SendBinFeedPositionCommand();
                }
                
                // 检测机械臂放置完成信号的上升沿（false → true）
                if (!_lastRobotPlaceCompletedSignal && currentPlaceSignal)
                {
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                        "检测到机械臂放置完成信号，发送料仓下料位置指令");
                    
                    // 发送料仓下料位置指令
                    SendBinDownPositionCommand();
                }
                
                // 更新信号状态缓存
                _lastRobotPickupCompletedSignal = currentPickupSignal;
                _lastRobotPlaceCompletedSignal = currentPlaceSignal;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "处理机械臂信号", ex.Message);
            }
        }

        /// <summary>
        /// 发送料仓上料位置指令
        /// </summary>
        private void SendBinFeedPositionCommand()
        {
            try
            {
                // 选择当前料仓
                int selectedBin = GetSelectedBin();
                
                // 创建上料位置指令
                var commandMsg = new BinElevatorCommandMessage(selectedBin, BinCommand.FeedPosition, 
                    "机械臂取料完成，料仓" + selectedBin + "执行上料位置");
                
                var message = new MessageModel(MsgSubject.BinElevatorCommand, commandMsg);
                _msgManager.PushMsg(message);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "已发送料仓" + selectedBin + "上料位置指令 (ID: " + commandMsg.CommandId + ")");
                
                // 激活料仓选择信号
                ActivateBinSelectionSignal(selectedBin);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "发送料仓上料位置指令", ex.Message);
            }
        }

        /// <summary>
        /// 发送料仓下料位置指令
        /// </summary>
        private void SendBinDownPositionCommand()
        {
            try
            {
                // 选择当前料仓
                int selectedBin = GetSelectedBin();
                
                // 创建下料位置指令
                var commandMsg = new BinElevatorCommandMessage(selectedBin, BinCommand.DownPosition, 
                    "机械臂放置完成，料仓" + selectedBin + "执行下料位置");
                
                var message = new MessageModel(MsgSubject.BinElevatorCommand, commandMsg);
                _msgManager.PushMsg(message);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "已发送料仓" + selectedBin + "下料位置指令 (ID: " + commandMsg.CommandId + ")");
                
                // 切换到下一个料仓（轮询方式）
                SwitchToNextBin();
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "发送料仓下料位置指令", ex.Message);
            }
        }

        #endregion

        #region 料仓选择逻辑

        /// <summary>
        /// 获取当前选择的料仓
        /// </summary>
        /// <returns>料仓编号</returns>
        private int GetSelectedBin()
        {
            return _currentBinIndex;
        }

        /// <summary>
        /// 切换到下一个料仓（轮询方式：1→2→3→1→...）
        /// </summary>
        private void SwitchToNextBin()
        {
            int previousBin = _currentBinIndex;
            
            _currentBinIndex = (_currentBinIndex % 3) + 1; // 1→2→3→1→...
            
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                "料仓选择切换: " + previousBin + " → " + _currentBinIndex);
        }

        /// <summary>
        /// 激活料仓选择信号
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        private void ActivateBinSelectionSignal(int binNumber)
        {
            try
            {
                if (_ioManager == null || !_ioManager.IsConnected)
                {
                    return;
                }

                // 根据IO表，料仓选择信号输出索引：
                // OUT11: 料仓1选择信号 (LogicalIndex 11)
                // OUT12: 料仓2选择信号 (LogicalIndex 12) 
                // OUT13: 料仓3选择信号 (LogicalIndex 13)
                
                // 先清除所有料仓选择信号
                _ioManager.LayeredIO.WriteOutBit(11, false); // 料仓1选择信号
                _ioManager.LayeredIO.WriteOutBit(12, false); // 料仓2选择信号
                _ioManager.LayeredIO.WriteOutBit(13, false); // 料仓3选择信号
                
                // 激活指定料仓的选择信号
                int outputIndex = 10 + binNumber; // 11, 12, 13
                _ioManager.LayeredIO.WriteOutBit(outputIndex, true);
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "激活料仓" + binNumber + "选择信号 (OUT" + outputIndex + ")");
                
                // 设定信号持续时间（例如100ms后自动关闭）
                System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
                {
                    try
                    {
                        if (_ioManager != null && _ioManager.IsConnected)
                        {
                            _ioManager.LayeredIO.WriteOutBit(outputIndex, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                            "关闭料仓选择信号", ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "激活料仓" + binNumber + "选择信号", ex.Message);
            }
        }

        #endregion

        #region IO信号处理

        /// <summary>
        /// 读取IO信号状态
        /// </summary>
        /// <param name="logicalIndex">逻辑索引</param>
        /// <returns>信号状态</returns>
        private bool ReadIOSignal(int logicalIndex)
        {
            try
            {
                if (_ioManager == null || !_ioManager.IsConnected)
                {
                    return false;
                }
                
                return _ioManager.LayeredIO.ReadInBit(logicalIndex, true);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.IOReadError, 
                    "IO信号IN" + logicalIndex, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 初始化IO信号状态
        /// </summary>
        private void InitializeIOStates()
        {
            try
            {
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, "初始化IO信号状态");
                
                // 读取当前IO信号状态
                _lastRobotPickupCompletedSignal = ReadIOSignal(10);  // IN10
                _lastRobotPlaceCompletedSignal = ReadIOSignal(8);    // IN8
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, 
                    "IO信号状态初始化完成 - IN10(取料):" + (_lastRobotPickupCompletedSignal ? "高" : "低") + 
                    " IN8(放置):" + (_lastRobotPlaceCompletedSignal ? "高" : "低"));
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, "IO信号状态初始化", ex.Message);
            }
        }

        #endregion

        #region 消息处理

        /// <summary>
        /// 处理系统状态变化消息
        /// </summary>
        private void OnSystemStatusChanged(MessageModel msg)
        {
            try
            {
                if (msg.Data is SystemStatusMessage statusMsg)
                {
                    lock (_stateLock)
                    {
                        switch (statusMsg.ChangeType)
                        {
                            case SystemStatusChangeType.SystemStarted:
                                _systemStarted = statusMsg.IsStarted;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                                    "料仓控制器模块" + (statusMsg.IsStarted ? "启动" : "停止"));
                                break;
                                
                            case SystemStatusChangeType.SystemStopped:
                                _systemStarted = false;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                                    "料仓控制器模块停止");
                                break;
                                
                            case SystemStatusChangeType.SystemModeChanged:
                                _currentMode = statusMsg.SystemMode;
                                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                                    "料仓控制器模式切换到" + (statusMsg.SystemMode == SystemMode.Auto ? "自动" : "手动"));
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ProcessingError, 
                    "处理系统状态消息", ex.Message);
            }
        }

        #endregion

        #region 公共接口方法

        /// <summary>
        /// 手动切换料仓选择（测试用）
        /// </summary>
        /// <param name="binNumber">料仓编号</param>
        public void ManualSelectBin(int binNumber)
        {
            if (binNumber >= 1 && binNumber <= 3)
            {
                int previousBin = _currentBinIndex;
                _currentBinIndex = binNumber;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingStarted, 
                    "手动切换料仓选择: " + previousBin + " → " + _currentBinIndex);
            }
        }

        /// <summary>
        /// 获取当前料仓选择状态
        /// </summary>
        /// <returns>当前选择的料仓编号</returns>
        public int GetCurrentSelectedBin()
        {
            return _currentBinIndex;
        }

        /// <summary>
        /// 获取IO信号状态（调试用）
        /// </summary>
        /// <returns>IO信号状态字符串</returns>
        public string GetIOSignalStatus()
        {
            return "IN10(取料):" + (_lastRobotPickupCompletedSignal ? "高" : "低") + 
                   " IN8(放置):" + (_lastRobotPlaceCompletedSignal ? "高" : "低") + 
                   " 当前料仓:" + _currentBinIndex;
        }

        #endregion
    }
}