using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Model.IO;
using Ewan.Model.Messages;
using System;
using System.Threading;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Ewan.Core.Module
{
    /// <summary>
    /// IO轮询模块 - 每200ms轮询读取IO数据并发送更新消息
    /// </summary>
    public class IOPollingModule : BaseModule<IOPollingModule>
    {
        private LayeredIOManager _ioManager;
        private IOStatus _ioStatus;
        private readonly int _readInterval = 200; // 200ms读取间隔
        private DateTime _lastSyncTime;
        private static bool _useMappingMode = true; // 默认使用映射模式

        protected override void OnInit()
        {
            try
            {
                // 获取IO管理器实例
                _ioManager = LayeredIOManager.Instance();
                
                // 创建IOStatus实例
                _ioStatus = new IOStatus();
                _ioStatus.HardwareType = "LayeredIO";
                
                // 从配置文件加载IO点位的映射名称
                LoadIOPointMappingNames();
                
                _lastSyncTime = DateTime.Now;
                
                _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "IOPollingModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "IOPollingModule", ex.Message);
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
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "IOPollingModule", ex.Message);
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
                _ioStatus.IsConnected = false;
                _ioStatus.ErrorMessage = "IO管理器未连接";
                return;
            }

            try
            {
                var layeredIO = _ioManager.LayeredIO;
                if (layeredIO == null || !layeredIO.IsOpen)
                {
                    _ioStatus.IsConnected = false;
                    _ioStatus.ErrorMessage = "LayeredIO未打开";
                    return;
                }

                // 直接读取所有输入点 (X1-X64)，SafetyModule已经执行了DataSync
                // 根据模式读取真实值(false)或映射值(true)
                for (int i = 0; i < IOStatus.IO_COUNT; i++)
                {
                    _ioStatus.X[i] = layeredIO.ReadInBit(i, _useMappingMode);
                }

                // 读取所有输出点 (Y1-Y64)
                for (int i = 0; i < IOStatus.IO_COUNT; i++)
                {
                    _ioStatus.Y[i] = layeredIO.ReadOutBit(i, _useMappingMode);
                }

                // 更新状态
                _ioStatus.IsConnected = true;
                _ioStatus.ErrorMessage = string.Empty;
                _ioStatus.LastUpdateTime = DateTime.Now;

                // 发送消息通知UI更新
                SendIOUpdateMessage();
            }
            catch (Exception ex)
            {
                _ioStatus.IsConnected = false;
                _ioStatus.ErrorMessage = ex.Message;
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "IOPollingModule IO Read", ex.Message);
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
                var message = new MessageModel(MsgSubject.IOUpdate, _ioStatus);
                MsgManager.Instance().PushMsg(message);
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, "IOPollingModule Message Send", ex.Message);
            }
        }

        /// <summary>
        /// 获取当前的IOStatus数据
        /// </summary>
        /// <returns>IOStatus实例</returns>
        public IOStatus GetIOStatus()
        {
            return _ioStatus;
        }

        /// <summary>
        /// 设置输出点值
        /// </summary>
        /// <param name="index">输出点索引 (0-63)</param>
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
                bool result = layeredIO.WriteOutBit(index, value);
                
                if (result)
                {
                    // 更新本地缓存
                    _ioStatus.SetY(index, value);
                }

                return result;
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, $"IOPollingModule Write Y{index}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 设置是否使用映射模式
        /// </summary>
        /// <param name="useMapping">true: 使用映射值, false: 使用真实值</param>
        public static void SetMappingMode(bool useMapping)
        {
            _useMappingMode = useMapping;
        }

        /// <summary>
        /// 获取当前是否使用映射模式
        /// </summary>
        public static bool IsMappingMode => _useMappingMode;

        /// <summary>
        /// 从映射配置文件加载IO点位名称
        /// </summary>
        private void LoadIOPointMappingNames()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "io_mapping.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    dynamic config = JsonConvert.DeserializeObject(json);
                    
                    // 读取输入映射名称
                    if (config?.InputMappings != null)
                    {
                        foreach (var mapping in config.InputMappings)
                        {
                            int logicalIndex = (int)mapping.LogicalIndex;
                            string name = (string)mapping.Name;
                            if (logicalIndex >= 0 && logicalIndex < IOStatus.IO_COUNT && !string.IsNullOrEmpty(name))
                            {
                                _ioStatus.XNames[logicalIndex] = name;
                            }
                        }
                    }
                    
                    // 读取输出映射名称
                    if (config?.OutputMappings != null)
                    {
                        foreach (var mapping in config.OutputMappings)
                        {
                            int logicalIndex = (int)mapping.LogicalIndex;
                            string name = (string)mapping.Name;
                            if (logicalIndex >= 0 && logicalIndex < IOStatus.IO_COUNT && !string.IsNullOrEmpty(name))
                            {
                                _ioStatus.YNames[logicalIndex] = name;
                            }
                        }
                    }
                    
                    _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleInitialized, "IOPollingModule: IO映射名称加载成功");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleInitializationFailed, "IOPollingModule: IO映射名称加载失败", ex.Message);
                // 使用默认名称
            }
        }

        protected override void OnDestroy()
        {
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ModuleDestroyed, "IOPollingModule");
        }
    }
}