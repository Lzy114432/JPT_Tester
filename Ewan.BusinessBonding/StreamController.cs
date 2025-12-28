using EwanCore;
using EwanCore.Attribute;
using EwanCore.Messaging;
using EwanCore.AlarmSystem;
using EwanCommon.Logging;
using log4net;
using Ewan.Core.Module;
using Ewan.Core.Module.Interface;
using Ewan.Core.Manager;
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
    public class StreamController : IManager
    {
        private static readonly ILog s_logger = Log.GetLogger(typeof(StreamController));
        private bool _disposed;

        #region 单例支持
        private static readonly Lazy<StreamController> s_instance = new Lazy<StreamController>(() => new StreamController());
        public static StreamController Instance() => s_instance.Value;
        #endregion

        #region 流程runner

        /// <summary>
        /// 主逻辑管理器（替代原 ProductionLineOperator）
        /// </summary>
        private LogicManager _productionOperator;

        /// <summary>
        /// PLC心跳流程runner
        /// </summary>
        private StreamRunner _plcHeartRunner;

        /// <summary>
        /// 安全流程runner
        /// </summary>
        private StreamRunner _safetyRunner;

        /// <summary>
        /// 料仓升降控制流程runner
        /// </summary>
        private StreamRunner _binElevatorRunner;

        /// <summary>
        /// 系统状态指示器流程runner
        /// </summary>
        private StreamRunner _statusIndicatorRunner;

        /// <summary>
        /// 皮带输送控制流程runner
        /// </summary>
        private StreamRunner _beltConveyorRunner;

        /// <summary>
        /// Station心跳流程runner
        /// </summary>
        private StreamRunner _stationHeartbeatRunner;

        /// <summary>
        /// 环线通信流程runner
        /// </summary>
        private StreamRunner _ringLineRunner;

        /// <summary>
        /// MES消息处理流程runner
        /// </summary>
        private StreamRunner _mesRunner;

        /// <summary>
        /// 报警流程runner（暂时注释，调试时启用）
        /// </summary>
        // private StreamRunner _alarmRunner;

        #endregion

        #region 节点集合

        // 主流程改为使用 ProductionLineOperator，不再需要 _mainModules
        private List<IModule> _plcHeartModules = new List<IModule>();
        private List<IModule> _safetyModules = new List<IModule>();
        private List<IModule> _binElevatorModules = new List<IModule>();
        private List<IModule> _statusIndicatorModules = new List<IModule>();
        private List<IModule> _beltConveyorModules = new List<IModule>();
        private List<IModule> _stationHeartbeatModules = new List<IModule>();
        private List<IModule> _ringLineModules = new List<IModule>();
        private List<IModule> _mesModules = new List<IModule>();

        /// <summary>
        /// 报警模块集合（暂时注释，调试时启用）
        /// </summary>
        // private List<IModule> _alarmModules = new List<IModule>();

        #endregion

        public bool Init()
        {
            s_logger.Info("StreamController 初始化开始");

            #region  //构造系统状态指示器流程的节点并加入到对应runner

            // 添加系统状态指示器模块，统一控制三色灯和蜂鸣器
            _statusIndicatorModules.Add(new SystemStatusIndicatorModule());

            // 创建系统状态指示器流程runner
            _statusIndicatorRunner = new StreamRunner(_statusIndicatorModules);

            #endregion

            #region  //构造主流程的节点并加入到对应runner

            _productionOperator = LogicManager.Instance();

            // 订阅报警事件（可用于UI显示）
            _productionOperator.Alarms.AlarmChanged += OnAlarmChanged;

            s_logger.Info("LogicManager 绑定完成");

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

            #region //构造料仓升降控制流程的节点并加入到对应runner（已合并到主流程）

            // BinElevatorModule已合并到主流程，此流程不再使用
            // _binElevatorModules.Add(new BinElevatorModule());
            // _binElevatorRunner = new StreamRunner(_binElevatorModules);

            #endregion

            #region //构造皮带输送控制流程的节点并加入到对应runner

            // 添加皮带输送控制模块，在自动模式下控制皮带持续运行
            _beltConveyorModules.Add(new BeltConveyorModule());

            // 创建皮带输送控制流程runner
            _beltConveyorRunner = new StreamRunner(_beltConveyorModules);

            #endregion

            #region //构造Station心跳流程的节点并加入到对应runner

            // 添加Station心跳模块，每1秒向寄存器170写入1
            _stationHeartbeatModules.Add(new StationHeartbeatModule());

            // 创建Station心跳流程runner
            _stationHeartbeatRunner = new StreamRunner(_stationHeartbeatModules);

            #endregion

            #region //构造环线通信流程的节点并加入到对应runner

            // 添加环线通信模块，读取寄存器152 (环线要料信号)
            _ringLineModules.Add(new RingLineModule());

            // 创建环线通信流程runner
            _ringLineRunner = new StreamRunner(_ringLineModules);

            #endregion

            #region //构造MES消息处理流程的节点并加入到对应runner

            // 添加MES消息处理模块（监听MesRequest并回推MesFeedback）
            _mesModules.Add(new MesModule());

            // 创建MES消息处理流程runner（独立线程运行，避免阻塞主流程）
            _mesRunner = new StreamRunner(_mesModules);

            #endregion

            #region //构造报警流程的节点并加入到对应runner（暂时注释，调试时启用）

            // 极简的报警系统 - 只检测信号和执行停机
            // _alarmModules.Add(new AlarmDataCollectorModule());    // 检测报警输入信号
            // _alarmModules.Add(new AlarmProcessorModule());        // 执行停机控制

            // 创建报警流程runner
            // _alarmRunner = new StreamRunner(_alarmModules);

            // 注意：三色灯由SystemStatusIndicatorModule统一控制

            #endregion

            s_logger.Info("StreamController 初始化完成");
            return true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            s_logger.Info("StreamController 开始销毁");

            try
            {
                StopRun();

                _productionOperator = null;
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("StreamController 销毁时停止流程出错: {0}", ex.Message);
            }

            s_logger.Info("StreamController 销毁完成");
        }

        [Obsolete("请使用 Dispose() 方法")]
        public void Destroy() => Dispose();
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

                // 通过消息队列通知系统状态变为待机（三色灯变为黄灯常亮）
                SendSystemStatusMessage(SystemStatus.Standby, "流程启动 - 待机状态");

                //2.运行plc心跳流程
                StartPlcHeartStream();
                //3.运行主流程
                StartMainStream();
                //4.运行料仓升降控制流程
                StartBinElevatorStream();
                //5.运行皮带输送控制流程
                StartBeltConveyorStream();
                //6.运行Station心跳流程
                StartStationHeartbeatStream();
                //7.运行环线通信流程
                StartRingLineStream();
                //8.运行MES消息处理流程
                StartMesStream();
                //9.运行报警流程（暂时注释，调试时启用）
                //StartAlarmStream();

                ////8.运行plc产能统计流程
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
            //4.停止皮带输送流程
            StopBeltConveyorStream();
            //5.停止Station心跳流程
            StopStationHeartbeatStream();
            //6.停止环线通信流程
            StopRingLineStream();
            //7.停止MES消息处理流程
            StopMesStream();
            //7.停止报警流程（暂时注释，调试时启用）
            //StopAlarmStream();

            // 通过消息队列通知系统状态变为停止（关闭所有指示灯）
            SendSystemStatusMessage(SystemStatus.Stopped, "流程停止");

            //7.停止系统状态指示器流程
            StopStatusIndicatorStream();
            //8.停止安全流程（最后停止，确保安全监控到最后一刻）
            StopSafetyStream();
            //StopAlarmStream();
            


            ////...n.停止其他流程
            //StopOtherStream();
        }

        /// <summary>
        /// 启动主流程（使用 LogicManager）
        /// </summary>
        private void StartMainStream()
        {
            if (_productionOperator != null)
            {
                bool started = _productionOperator.Start();
                if (started)
                {
                    s_logger.Info("主流程（LogicManager）已启动");
                }
                else
                {
                    s_logger.Warn("主流程启动失败，可能存在报警/需要复位");
                }
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
        /// 停止主流程（使用 LogicManager）
        /// </summary>
        private void StopMainStream()
        {
            _productionOperator?.Stop();
            s_logger.Info("主流程（LogicManager）已停止");
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
        /// 启动料仓升降流程
        /// </summary>
        private void StartBinElevatorStream()
        {
            if (_binElevatorRunner != null)
            {
                _binElevatorRunner.Start();
                s_logger.Debug("料仓升降控制流程已启动");
            }
        }

        /// <summary>
        /// 停止料仓升降流程
        /// </summary>
        private void StopBinElevatorStream()
        {
            _binElevatorRunner?.Stop();
            s_logger.Debug("料仓升降控制流程已停止");
        }

        /// <summary>
        /// 启动皮带输送流程
        /// </summary>
        private void StartBeltConveyorStream()
        {
            if (_beltConveyorRunner != null)
            {
                _beltConveyorRunner.Start();
                s_logger.Debug("皮带输送控制流程已启动");
            }
        }

        /// <summary>
        /// 停止皮带输送流程
        /// </summary>
        private void StopBeltConveyorStream()
        {
            _beltConveyorRunner?.Stop();
            s_logger.Debug("皮带输送控制流程已停止");
        }

        /// <summary>
        /// 启动Station心跳流程
        /// </summary>
        private void StartStationHeartbeatStream()
        {
            if (_stationHeartbeatRunner != null)
            {
                _stationHeartbeatRunner.Start();
                s_logger.Debug("Station心跳流程已启动");
            }
        }

        /// <summary>
        /// 停止Station心跳流程
        /// </summary>
        private void StopStationHeartbeatStream()
        {
            _stationHeartbeatRunner?.Stop();
            s_logger.Debug("Station心跳流程已停止");
        }

        /// <summary>
        /// 启动环线通信流程
        /// </summary>
        private void StartRingLineStream()
        {
            if (_ringLineRunner != null)
            {
                _ringLineRunner.Start();
                s_logger.Debug("环线通信流程已启动");
            }
        }

        /// <summary>
        /// 停止环线通信流程
        /// </summary>
        private void StopRingLineStream()
        {
            _ringLineRunner?.Stop();
            s_logger.Debug("环线通信流程已停止");
        }

        /// <summary>
        /// 启动MES消息处理流程
        /// </summary>
        private void StartMesStream()
        {
            if (_mesRunner != null)
            {
                _mesRunner.Start();
                s_logger.Debug("MES消息处理流程已启动");
            }
        }

        /// <summary>
        /// 停止MES消息处理流程
        /// </summary>
        private void StopMesStream()
        {
            _mesRunner?.Stop();
            s_logger.Debug("MES消息处理流程已停止");
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
                s_logger.Debug("系统状态指示器流程已启动");
            }
        }

        /// <summary>
        /// 停止系统状态指示器流程
        /// </summary>
        private void StopStatusIndicatorStream()
        {
            _statusIndicatorRunner?.Stop();
            s_logger.Debug("系统状态指示器流程已停止");
        }

        /// <summary>
        /// 通过消息队列发送系统状态变化通知
        /// </summary>
        private void SendSystemStatusMessage(SystemStatus status, string description, bool isCritical = false)
        {
            try
            {
                var command = new StatusIndicatorCommand(status, description, isCritical);
                MessageHub.Current.Post(command);

                s_logger.DebugFormat("发送系统状态消息: {0} - {1}", status, description);
            }
            catch (Exception ex)
            {
                s_logger.ErrorFormat("StreamController-SendSystemStatusMessage 错误: {0}", ex.Message);
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

        #region ProductionLineOperator 公共控制方法

        /// <summary>
        /// 获取生产线操作器（供外部访问）
        /// </summary>
        public LogicManager ProductionOperator => _productionOperator;

        /// <summary>
        /// 获取报警服务
        /// </summary>
        public IAlarmService Alarms => _productionOperator?.Alarms;

        /// <summary>
        /// 暂停生产线
        /// </summary>
        public void PauseProduction() => _productionOperator?.Pause();

        /// <summary>
        /// 恢复生产线
        /// </summary>
        public void ResumeProduction() => _productionOperator?.Resume();

        /// <summary>
        /// 紧急停止
        /// </summary>
        public void EmergencyStop() => _productionOperator?.EmergencyStop();

        /// <summary>
        /// 复位/回原（统一入口：转发到 LogicManager.Home()）
        /// </summary>
        /// <param name="clearAlarm">是否清除报警</param>
        public void Home(bool clearAlarm = true)
        {
            if (clearAlarm)
            {
                _productionOperator?.ClearAlarm();
            }

            // 核心复位逻辑由 LogicManager 统一管理
            _productionOperator?.Home();
        }

        /// <summary>
        /// 清除报警
        /// </summary>
        public void ClearAlarm() => _productionOperator?.ClearAlarm();

        /// <summary>
        /// 报警变化事件处理
        /// </summary>
        private void OnAlarmChanged(object sender, AlarmChangedEventArgs e)
        {
            var key = e.Alarm?.Key ?? "(null)";
            var content = e.Alarm?.Content ?? "(cleared)";
            s_logger.InfoFormat("报警变化: kind={0}, key={1}, content={2}", e.Kind, key, content);

            // 如果有报警，发送系统状态消息
            if (e.Kind == AlarmChangeKind.Added && e.Alarm != null)
            {
                bool isCritical = e.Alarm.Level == AlarmLevel.H;
                var status = isCritical ? SystemStatus.Critical : SystemStatus.Alarm;
                SendSystemStatusMessage(status, e.Alarm.Content, isCritical);
            }
        }

        #endregion

    }
}
