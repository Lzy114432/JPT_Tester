using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Model.IO;
using Ewan.Model.Messages;
using System;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// 真实IO读取模块 - 每200ms读取IO数据并发送更新消息
    /// </summary>
    public class RealIOModule : BaseModule<RealIOModule>
    {
        private LayeredIOManager _ioManager;
        private RealIO _realIO;
        private readonly int _readInterval = 200; // 200ms读取间隔
        private DateTime _lastSyncTime;

        protected override void OnInit()
        {
            try
            {
                // 获取IO管理器实例
                _ioManager = LayeredIOManager.Instance();
                
                // 创建RealIO实例
                _realIO = new RealIO();
                _realIO.HardwareType = "LayeredIO";
                
                _lastSyncTime = DateTime.Now;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "RealIOModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "RealIOModule", ex.Message);
            }
        }

        protected override bool OnRun()
        {
            try
            {
                // 检查是否到达读取时间
                var now = DateTime.Now;
                if ((now - _lastSyncTime).TotalMilliseconds >= _readInterval)
                {
                    // 读取IO数据（SafetyModule已经同步）
                    ReadIOData();
                    _lastSyncTime = now;
                }
                else
                {
                    // 短暂休眠以减少CPU占用
                    Thread.Sleep(10);
                }

                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "RealIOModule", ex.Message);
                Thread.Sleep(1000); // 出错时等待1秒
                return true; // 继续运行
            }
        }

        /// <summary>
        /// 读取IO数据（从已同步的LayeredIO读取）
        /// </summary>
        private void ReadIOData()
        {
            if (_ioManager == null || !_ioManager.IsConnected)
            {
                _realIO.IsConnected = false;
                _realIO.ErrorMessage = "IO管理器未连接";
                return;
            }

            try
            {
                var layeredIO = _ioManager.LayeredIO;
                if (layeredIO == null || !layeredIO.IsOpen)
                {
                    _realIO.IsConnected = false;
                    _realIO.ErrorMessage = "LayeredIO未打开";
                    return;
                }

                // 直接读取所有输入点 (X1-X64)，SafetyModule已经执行了DataSync
                for (int i = 0; i < RealIO.IO_COUNT; i++)
                {
                    _realIO.X[i] = layeredIO.ReadInBit(i,false);
                }

                // 读取所有输出点 (Y1-Y64)
                for (int i = 0; i < RealIO.IO_COUNT; i++)
                {
                    _realIO.Y[i] = layeredIO.ReadOutBit(i, false);
                }

                // 更新状态
                _realIO.IsConnected = true;
                _realIO.ErrorMessage = string.Empty;
                _realIO.LastUpdateTime = DateTime.Now;

                // 发送消息通知UI更新
                SendIOUpdateMessage();
            }
            catch (Exception ex)
            {
                _realIO.IsConnected = false;
                _realIO.ErrorMessage = ex.Message;
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "RealIOModule IO Sync", ex.Message);
            }
        }

        /// <summary>
        /// 发送IO更新消息
        /// </summary>
        private void SendIOUpdateMessage()
        {
            try
            {
                // 创建IO更新消息
                var message = new MessageModel(MsgSubject.IOUpdate, _realIO);
                MsgManager.Instance().PushMsg(message);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "RealIOModule Message Send", ex.Message);
            }
        }

        /// <summary>
        /// 获取当前的RealIO数据
        /// </summary>
        /// <returns>RealIO实例</returns>
        public RealIO GetRealIO()
        {
            return _realIO;
        }

        /// <summary>
        /// 设置输出点值
        /// </summary>
        /// <param name="index">输出点索引 (1-64)</param>
        /// <param name="value">输出值</param>
        public bool SetOutput(int index, bool value)
        {
            try
            {
                if (_ioManager == null || !_ioManager.IsConnected)
                    return false;

                var layeredIO = _ioManager.LayeredIO;
                if (layeredIO == null || !layeredIO.IsOpen)
                    return false;

                // 写入输出点 (索引从0开始)
                bool result = layeredIO.WriteOutBit(index - 1, value);
                
                if (result)
                {
                    // 更新本地缓存
                    _realIO.SetY(index, value);
                }

                return result;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, $"RealIOModule Write Y{index}", ex.Message);
                return false;
            }
        }

        protected override void OnDestroy()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "RealIOModule");
        }
    }
}