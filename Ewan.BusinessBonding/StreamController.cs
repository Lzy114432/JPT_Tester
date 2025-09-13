using Ewan.Core;
using Ewan.Core.Attribute;
using Ewan.Core.Module;
using Ewan.Core.Module.Interface;
using Ewan.Core.Msg;
using Ewan.Core.Run;
using Ewan.Model.System;
using System;
using System.Collections.Generic;

namespace Ewan.BusinessBonding
{
    /// <summary>
    /// 流程运行控制器
    /// </summary>
    [Manager(Priority = 3)]
    public class StreamController : BaseManager<StreamController>
    {
        #region 流程runner

        /// <summary>
        /// 主流程runner
        /// </summary>
        private StreamRunner _mainRunner;

        /// <summary>
        /// PLC心跳流程runner
        /// </summary>
        private StreamRunner _plcHeartRunner;

        /// <summary>
        /// 安全流程runner
        /// </summary>
        private StreamRunner _safetyRunner;

        /// <summary>
        /// IO轮询流程runner
        /// </summary>
        private StreamRunner _ioPollingRunner;

        /// <summary>
        /// 料仓升降控制流程runner
        /// </summary>
        private StreamRunner _binElevatorRunner;

        /// <summary>
        /// 系统状态指示器流程runner
        /// </summary>
        private StreamRunner _statusIndicatorRunner;

        /// <summary>
        /// 报警流程runner（暂时注释，调试时启用）
        /// </summary>
        // private StreamRunner _alarmRunner;

        #endregion

        #region 节点集合

        private List<IModule> _mainModules = new List<IModule>();
        private List<IModule> _plcHeartModules = new List<IModule>();
        private List<IModule> _safetyModules = new List<IModule>();
        private List<IModule> _ioPollingModules = new List<IModule>();
        private List<IModule> _binElevatorModules = new List<IModule>();
        private List<IModule> _statusIndicatorModules = new List<IModule>();
        
        /// <summary>
        /// 报警模块集合（暂时注释，调试时启用）
        /// </summary>
        // private List<IModule> _alarmModules = new List<IModule>();

        #endregion

        public override bool Init()
        {
            #region  //构造系统状态指示器流程的节点并加入到对应runner
            
            // 添加系统状态指示器模块，统一控制三色灯和蜂鸣器
            _statusIndicatorModules.Add(new SystemStatusIndicatorModule());
            
            // 创建系统状态指示器流程runner
            _statusIndicatorRunner = new StreamRunner(_statusIndicatorModules);
            
            #endregion
            
            #region  //构造主流程的节点并加入到对应runner
            
            // 添加自动生产流程模块
            _mainModules.Add(new AutoProductionModule());
            
            //_mainModules.Add(new PlcModule());//测试可以换成数据模拟节点 根据配置决定加载哪个PLC节点
            //_mainModules.Add(new AlarmModule<PlcModel>());
            
            // 创建主流程runner
            _mainRunner = new StreamRunner(_mainModules);
            
            #endregion

            #region //构造plc心跳流程的节点并加入到对应runner

            //_plcHeartModules.Add(new PlcHeartModule());
            //_plcHeartRunner = new StreamRunner(_plcHeartModules);

            #endregion
                
            #region //构造安全流程的节点并加入到对应runner
            
            // 添加SafetyModule用于IO数据同步
            _safetyModules.Add(new SafetyModule());
            
            // 创建安全流程runner
            _safetyRunner = new StreamRunner(_safetyModules);
            
            #endregion

            #region //构造IO轮询流程的节点并加入到对应runner
            
            // 添加IOPollingModule用于IO数据轮询（200ms）
            _ioPollingModules.Add(new IOPollingModule());
            
            // 创建IO轮询流程runner
            _ioPollingRunner = new StreamRunner(_ioPollingModules);
            
            #endregion

            #region //构造料仓升降控制流程的节点并加入到对应runner
            
            // 添加BinElevatorModule用于料仓升降控制
            _binElevatorModules.Add(new BinElevatorModule());
            
            // 创建料仓升降流程runner
            _binElevatorRunner = new StreamRunner(_binElevatorModules);
            
            #endregion

            #region //构造报警流程的节点并加入到对应runner（暂时注释，调试时启用）
            
            // 极简的报警系统 - 只检测信号和执行停机
            // _alarmModules.Add(new AlarmDataCollectorModule());    // 检测报警输入信号
            // _alarmModules.Add(new AlarmProcessorModule());        // 执行停机控制
            
            // 创建报警流程runner
            // _alarmRunner = new StreamRunner(_alarmModules);
            
            // 注意：三色灯由SystemStatusIndicatorModule统一控制
            
            #endregion

            return base.Init();
        }
        /// <summary>
        /// 开启运行.
        /// </summary>
        public void StartRun()
        {
            try
            {
                //0.启动安全流程（最高优先级，负责安全监控）
                StartSafetyStream();
                
                //1.启动系统状态指示器流程（第二优先级，用于状态显示）
                StartStatusIndicatorStream();

                // 通过消息队列通知系统状态变为运行中（三色灯变为绿灯闪烁）
                SendSystemStatusMessage(SystemStatus.Running, "流程启动");

                //2.运行IO轮询流程（第三优先级，为其他流程提供IO数据）
                StartIOPollingStream();
                //3.运行plc心跳流程
                StartPlcHeartStream();
                //4.运行主流程
                StartMainStream();
                //5.运行料仓升降控制流程
                StartBinElevatorStream();
                //6.运行报警流程（暂时注释，调试时启用）
                //StartAlarmStream();
                
                ////6.运行plc产能统计流程
                //StartPlcProductCapacityStream();

                ////...n.运行其他流程
                //StartOtherStream();
            }
            catch (Exception ex)
            {
                ////s_logger.Error($"StartRun occur exception:{ex}");
                ////停止流程
                //StopMainStream();
                //StopPlcHeartStream();
                //StopSafetyStream();
                //StopPlcProductCapacityStream();
                //StopOtherStream();
            }
        }

        ///// <summary>
        /// 停止运行
        /// </summary>
        public void StopRun()
        {
            //1.停止主流程（先停止业务流程）
            StopMainStream();
            //2.停止plc心跳流程
            StopPlcHeartStream();
            //3.停止料仓升降流程
            StopBinElevatorStream();
            //4.停止报警流程（暂时注释，调试时启用）
            //StopAlarmStream();
            //5.停止IO轮询流程
            StopIOPollingStream();


            // 通过消息队列通知系统状态变为停止（关闭所有指示灯）
            SendSystemStatusMessage(SystemStatus.Stopped, "流程停止");

            //6.停止系统状态指示器流程
            StopStatusIndicatorStream();
            //7.停止安全流程（最后停止，确保安全监控到最后一刻）
            StopSafetyStream();
            //StopAlarmStream();
            


            ////...n.停止其他流程
            //StopOtherStream();
        }

        /// <summary>
        /// 启动主流程
        /// </summary>
        private void StartMainStream()
        {
            if (_mainRunner != null)
            {
                _mainRunner.Start();
            }
        }


        /// <summary>
        /// 启动心跳流程
        /// </summary>
        private void StartPlcHeartStream()
        {
            if (_plcHeartRunner != null)
            {
                _plcHeartRunner.Start();
            }
        }

        /// <summary>
        /// 启动安全流程
        /// </summary>
        private void StartSafetyStream()
        {
            if (_safetyRunner != null)
            {
                _safetyRunner.Start();
            }
        }

        /// <summary>
        /// 启动IO轮询流程
        /// </summary>
        private void StartIOPollingStream()
        {
            if (_ioPollingRunner != null)
            {
                _ioPollingRunner.Start();
            }
        }

        ///// <summary>
        ///// 启动产能流程
        ///// </summary>
        //private void StartPlcProductCapacityStream()
        //{
        //    if (_plcProductCapacityRunner != null)
        //    {
        //        _plcProductCapacityRunner.Start();
        //    }
        //}

        /// <summary>
        /// 停止主流程
        /// </summary>
        private void StopMainStream()
        {
            _mainRunner?.Stop();
        }

        /// <summary>
        /// 停止心跳流程
        /// </summary>
        private void StopPlcHeartStream()
        {
            _plcHeartRunner?.Stop();
        }

        /// <summary>
        /// 停止安全流程
        /// </summary>
        private void StopSafetyStream()
        {
            _safetyRunner?.Stop();
        }

        /// <summary>
        /// 停止IO轮询流程
        /// </summary>
        private void StopIOPollingStream()
        {
            _ioPollingRunner?.Stop();
        }

        /// <summary>
        /// 启动料仓升降流程
        /// </summary>
        private void StartBinElevatorStream()
        {
            if (_binElevatorRunner != null)
            {
                _binElevatorRunner.Start();
                _uiLogger.Info(() => "料仓升降控制流程已启动");
            }
        }

        /// <summary>
        /// 停止料仓升降流程
        /// </summary>
        private void StopBinElevatorStream()
        {
            _binElevatorRunner?.Stop();
            _uiLogger.Info(() => "料仓升降控制流程已停止");
        }

        /// <summary>
        /// 启动报警流程（暂时注释，调试时启用）
        /// </summary>
        //private void StartAlarmStream()
        //{
        //    if (_alarmRunner != null)
        //    {
        //        _alarmRunner.Start();
        //        // 报警流程应该在数据源流程之后启动
        //        _uiLogger.Info(() => "报警流程已启动");
        //    }
        //}

        /// <summary>
        /// 停止报警流程（暂时注释，调试时启用）
        /// </summary>
        //private void StopAlarmStream()
        //{
        //    _alarmRunner?.Stop();
        //    _uiLogger.Info(() => "报警流程已停止");
        //}

        /// <summary>
        /// 启动系统状态指示器流程
        /// </summary>
        private void StartStatusIndicatorStream()
        {
            if (_statusIndicatorRunner != null)
            {
                _statusIndicatorRunner.Start();
                _uiLogger.Info(() => "系统状态指示器流程已启动");
            }
        }

        /// <summary>
        /// 停止系统状态指示器流程
        /// </summary>
        private void StopStatusIndicatorStream()
        {
            _statusIndicatorRunner?.Stop();
            _uiLogger.Info(() => "系统状态指示器流程已停止");
        }

        /// <summary>
        /// 通过消息队列发送系统状态变化通知
        /// </summary>
        private void SendSystemStatusMessage(SystemStatus status, string description, bool isCritical = false)
        {
            try
            {
                var command = new StatusIndicatorCommand(status, description, isCritical);
                var message = new MessageModel(MsgSubject.StatusIndicator, command);
                
                MsgManager.Instance().PushMsg(message);
                
                _uiLogger.Info(() => $"发送系统状态消息: {status} - {description}");
            }
            catch (Exception ex)
            {
                _uiLogger.Error(() => Ewan.Resources.LogMessages.ModuleRunError, 
                    "StreamController-SendSystemStatusMessage", ex.Message);
            }
        }

        ///// <summary>
        ///// 停止心跳流程
        ///// </summary>
        //private void StopPlcProductCapacityStream()
        //{
        //    _plcProductCapacityRunner.Stop();
        //}

        ///// <summary>
        ///// 启动其他流程
        ///// </summary>
        //private void StartOtherStream()
        //{
        //    _otherRunner.Start();
        //}

        ///// <summary>
        ///// 停止主流程
        ///// </summary>
        //private void StopOtherStream()
        //{
        //    _otherRunner.Stop();
        //}


    }
}
