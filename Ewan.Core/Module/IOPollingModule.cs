using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Model.IO;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;

namespace Ewan.Core.Module
{
    /// <summary>
    /// IO轮询模块 - 每200ms轮询读取IO数据并发送更新消息
    /// </summary>
    public class IOPollingModule : BaseModule<IOPollingModule>
    {
        //private LayeredIOManager _ioManager;
        private IOStatus _ioStatus;
        private readonly int _readInterval = 200; // 200ms读取间隔
        private DateTime _lastSyncTime;
        private static bool _useMappingMode = true; // 默认使用映射模式

        protected override void OnInit()
        {
            try
            {
                // 获取IO管理器实例
                //_ioManager = LayeredIOManager.Instance();
                
                // 创建IOStatus实例
                _ioStatus = new IOStatus();
                _ioStatus.HardwareType = "LayeredIO";
                
                // 从配置文件加载IO点位的映射名称
                LoadIOPointMappingNames();
                
                _lastSyncTime = DateTime.Now;
                
                _uiLogger.Info("模块初始化成功: {0}", "IOPollingModule");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("模块初始化失败: {0} - {1}", "IOPollingModule", ex.Message);
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
                _uiLogger.Error("模块运行错误: {0} - {1}", "IOPollingModule", ex.Message);
                Thread.Sleep(1000); // 出错时等待1秒
                return true; // 继续运行
            }
        }

        /// <summary>
        /// 读取IO数据（从已同步的LayeredIO读取）
        /// </summary>
        private void ReadIOData()
        {
            if (LayeredIOManager.Instance() == null || !LayeredIOManager.Instance().IsConnected)
            {
                _ioStatus.IsConnected = false;
                _ioStatus.ErrorMessage = "IO管理器未连接";
                return;
            }

            try
            {
                var layeredIO = LayeredIOManager.Instance().LayeredIO;
                if (layeredIO == null || !layeredIO.IsOpen)
                {
                    _ioStatus.IsConnected = false;
                    _ioStatus.ErrorMessage = "LayeredIO未打开";
                    return;
                }

                // 同时读取真实IO和映射IO数据
                for (int i = 0; i < IOStatus.IO_COUNT; i++)
                {
                    // 读取真实输入点（物理地址）
                    _ioStatus.XReal[i] = layeredIO.ReadInBit(i, false);
                    // 读取映射输入点（逻辑地址）
                    _ioStatus.XMapped[i] = layeredIO.ReadInBit(i, true);
                    
                    // 为了兼容旧代码，根据当前模式设置X数组
                    _ioStatus.X[i] = _useMappingMode ? _ioStatus.XMapped[i] : _ioStatus.XReal[i];
                }

                // 读取所有输出点
                for (int i = 0; i < IOStatus.IO_COUNT; i++)
                {
                    // 读取真实输出点（物理地址）
                    _ioStatus.YReal[i] = layeredIO.ReadOutBit(i, false);
                    // 读取映射输出点（逻辑地址）
                    _ioStatus.YMapped[i] = layeredIO.ReadOutBit(i, true);
                    
                    // 为了兼容旧代码，根据当前模式设置Y数组
                    _ioStatus.Y[i] = _useMappingMode ? _ioStatus.YMapped[i] : _ioStatus.YReal[i];
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
                _uiLogger.Error("模块运行错误: {0} - {1}", "IOPollingModule IO Read", ex.Message);
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
                _uiLogger.Error("模块运行错误: {0} - {1}", "IOPollingModule Message Send", ex.Message);
            }
        }

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
                                // 更新映射名称数组
                                _ioStatus.XMappedNames[logicalIndex] = name;
                                // 兼容旧代码
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
                                // 更新映射名称数组
                                _ioStatus.YMappedNames[logicalIndex] = name;
                                // 兼容旧代码
                                _ioStatus.YNames[logicalIndex] = name;
                            }
                        }
                    }
                    
                    _uiLogger.Info("模块初始化成功: {0}", "IOPollingModule: IO映射名称加载成功");
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error("模块初始化失败: {0} - {1}", "IOPollingModule: IO映射名称加载失败", ex.Message);
                // 使用默认名称
            }
        }

        protected override void OnDestroy()
        {
            _uiLogger.Info("模块已销毁: {0}", "IOPollingModule");
        }
    }
}