using System;
using System.Threading;
using Ewan.Core;
using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Model.Alarm;
using Ewan.Model.System;

namespace Ewan.BusinessBonding
{
    /// <summary>
    /// 系统控制业务服务
    /// 负责系统的启动、停止、状态管理等核心业务逻辑
    /// </summary>
    public class SystemControlService : BaseManager<SystemControlService>
    {
        private const int OUT_START = 5;
        private const int OUT_STOP = 6;
        private const int OUT_RECOVERY = 7;
        private const int OUT_PAUSE = 8;
        private const int DEFAULT_PULSE_WIDTH_MS = 200;
        private static readonly int[] SAFETY_DOOR_INPUTS =
        {
            AlarmIOMapping.SAFETY_DOOR1_ALARM,
            AlarmIOMapping.SAFETY_DOOR2_ALARM,
            AlarmIOMapping.SAFETY_DOOR3_ALARM
        };
        private readonly object _stateLock = new object();
        private bool _isPaused;

        public void InitializeSystem()
        {
            SendPulse(OUT_STOP);
            Thread.Sleep(DEFAULT_PULSE_WIDTH_MS);
            SendPulse(OUT_START);
            Push(SystemControlCommand.Initialize);
            UpdatePauseState(false);
            _uiLogger.InfoRaw("处理已完成: {0}", "系统初始化命令已发送");
        }

        public void StartSystem()
        {
            Push(SystemControlCommand.Start);
            UpdatePauseState(false);
            _uiLogger.InfoRaw("处理已完成: {0}", "系统启动命令已发送");

        }

        public void StopSystem()
        {
            SendPulse(OUT_STOP);
            Push(SystemControlCommand.Stop);
            UpdatePauseState(false);
            _uiLogger.InfoRaw("处理已完成: {0}", "系统停止命令已发送");

        }

        public void EmergencyStopSystem()
        {
            SendPulse(OUT_STOP);
            Push(SystemControlCommand.EmergencyStop);
            UpdatePauseState(false);
            _uiLogger.WarnRaw("处理已完成: {0}", "系统紧急停止命令已发送");
        }

        public void PauseSystem()
        {
            SendPulse(OUT_PAUSE);
            Push(SystemControlCommand.Pause);
            UpdatePauseState(true);
            _uiLogger.InfoRaw("处理已完成: {0}", "系统暂停命令已发送");
        }

        public void ResumeSystem()
        {
            Push(SystemControlCommand.Resume);
            UpdatePauseState(false);
            _uiLogger.InfoRaw("处理已完成: {0}", "系统恢复命令已发送");
        }

        public void SendRecoveryPulse()
        {
            SendPulse(OUT_RECOVERY);
        }

        public void SendStopPulse()
        {
            SendPulse(OUT_STOP);
        }

        public bool AreSafetyDoorsClosed()
        {
            var parameters = SystemParametersManager.Instance.Parameters;
            if (parameters.SafetyDoorAlarmBypass)
            {
                return true;
            }

            try
            {
                var ioManager = LayeredIOManager.Instance();
                if (ioManager == null || ioManager.LayeredIO == null)
                {
                    _uiLogger.WarnRaw("无法检测安全门状态: IO未初始化");
                    return false;
                }

                if (!ioManager.IsConnected)
                {
                    if (!ioManager.Connect())
                    {
                        _uiLogger.WarnRaw("无法检测安全门状态: IO未连接");
                        return false;
                    }
                }

                foreach (var input in SAFETY_DOOR_INPUTS)
                {
                    bool doorOpen = ioManager.LayeredIO.ReadInBit(input, true);
                    if (doorOpen)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("检测安全门状态失败: {0}", ex.Message);
                return false;
            }
        }

        public bool IsPaused()
        {
            lock (_stateLock)
            {
                return _isPaused;
            }
        }

        public void EnsurePauseRecoveryBeforeShutdown()
        {
            bool needRecovery;
            lock (_stateLock)
            {
                needRecovery = _isPaused;
                _isPaused = false;
            }

            if (!needRecovery)
            {
                return;
            }

            _uiLogger.InfoRaw("检测到系统处于暂停状态，关闭前执行停止与复原脉冲");
            SendStopPulse();
            Thread.Sleep(DEFAULT_PULSE_WIDTH_MS);
            SendRecoveryPulse();
        }




        /// <summary>
        /// 推送系统控制命令到消息队列
        /// </summary>
        /// <param name="systemControlCommand">系统控制命令</param>
        private void Push(SystemControlCommand systemControlCommand)
        {

            MessageModel msg = new MessageModel(MsgSubject.SystemControl, systemControlCommand);
            MsgManager.Instance().PushMsg(msg);

        }

        private void SendPulse(int outputIndex, int pulseWidthMs = DEFAULT_PULSE_WIDTH_MS)
        {
            try
            {
                var ioManager = LayeredIOManager.Instance();
                if (ioManager == null)
                {
                    _uiLogger.WarnRaw("发送脉冲失败: IO管理器未初始化");
                    return;
                }

                if (!ioManager.IsConnected)
                {
                    ioManager.Connect();
                }

                var layered = ioManager.LayeredIO;
                if (layered == null)
                {
                    _uiLogger.WarnRaw("发送脉冲失败: 未获取到LayeredIO实例");
                    return;
                }

                if (!layered.WriteOutBit(outputIndex, true, true))
                {
                    _uiLogger.WarnRaw("脉冲置位失败: Y{0}", outputIndex);
                    return;
                }

                Thread.Sleep(pulseWidthMs);
                layered.WriteOutBit(outputIndex, false, true);
                _uiLogger.DebugRaw("已发送脉冲: Y{0}", outputIndex);
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("发送脉冲异常: Y{0} - {1}", outputIndex, ex.Message);
            }
        }

        private void UpdatePauseState(bool paused)
        {
            lock (_stateLock)
            {
                _isPaused = paused;
            }
        }

    }
}