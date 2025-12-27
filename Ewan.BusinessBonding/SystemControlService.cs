using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ewan.Core;
using Ewan.Core.IO;
using Ewan.Core.Msg;
using Ewan.Model.IO;
using Ewan.Model.System;
using EwanIO.Core.Attributes;

namespace Ewan.BusinessBonding
{
    /// <summary>
    /// 系统控制业务服务
    /// 负责系统的启动、停止、状态管理等核心业务逻辑
    /// </summary>
    public class SystemControlService : BaseManager<SystemControlService>
    {
        private const int DEFAULT_PULSE_WIDTH_MS = 200;
        private readonly object _stateLock = new object();
        private bool _isPaused;

        public void InitializeSystem()
        {
            SendPulse(x => x.停止输出, now: true);
            Thread.Sleep(DEFAULT_PULSE_WIDTH_MS);
            SendPulse(x => x.开始);
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
            SendPulse(x => x.停止输出, now: true);
            Push(SystemControlCommand.Stop);
            UpdatePauseState(false);
            _uiLogger.InfoRaw("处理已完成: {0}", "系统停止命令已发送");

        }

        public void EmergencyStopSystem()
        {
            SendPulse(x => x.停止输出, now: true);
            Push(SystemControlCommand.EmergencyStop);
            UpdatePauseState(false);
            _uiLogger.WarnRaw("处理已完成: {0}", "系统紧急停止命令已发送");
        }

        public void PauseSystem()
        {
            SendPulse(x => x.暂停);
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
            SendPulse(x => x.复位);
        }

        public void SendStopPulse()
        {
            SendPulse(x => x.停止输出, now: true);
        }

        public void SetHighSpeedMode(bool enabled)
        {
            try
            {
                var ioManager = LayeredIOManager.Instance();
                if (ioManager == null)
                {
                    _uiLogger.WarnRaw("设置速度模式失败: IO管理器未初始化");
                    return;
                }

                if (!ioManager.IsConnected)
                {
                    ioManager.Connect();
                }

                var ctx = ioManager.Ctx;
                if (ctx == null)
                {
                    _uiLogger.WarnRaw("设置速度模式失败: 未获取到IO上下文实例");
                    return;
                }

                if (enabled)
                {
                    ctx.On(x => x.高速运行);
                }
                else
                {
                    ctx.Off(x => x.高速运行);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("设置速度模式失败: {0}", ex.Message);
            }
        }

        public bool ReadInitializeSignal()
        {
            try
            {
                var ctx = LayeredIOManager.Instance()?.Ctx;
                if (ctx == null)
                {
                    return false;
                }

                return ctx.R.初始化信号;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("读取初始化信号失败: {0}", ex.Message);
                return false;
            }
        }

        public async Task<bool> ClearAlarm(int pulseWidthMs = 100)
        {
            try
            {
                var ioManager = LayeredIOManager.Instance();
                if (ioManager == null)
                {
                    _uiLogger.WarnRaw("清除报警失败: IO管理器未初始化");
                    return false;
                }

                if (!ioManager.IsConnected)
                {
                    if (!ioManager.Connect())
                    {
                        _uiLogger.WarnRaw("清除报警失败: IO未连接");
                        return false;
                    }
                }

                var ctx = ioManager.Ctx;
                if (ctx == null)
                {
                    _uiLogger.WarnRaw("清除报警失败: 未获取到IO上下文实例");
                    return false;
                }

                ctx.On(x => x.清除报警, now: true);
                await Task.Delay(pulseWidthMs);
                ctx.Off(x => x.清除报警, now: true);

                return true;
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("清除报警异常: {0}", ex.Message);
                return false;
            }
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
                if (ioManager == null || ioManager.Ctx == null)
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

                var ctx = ioManager.Ctx;
                if (ctx.R.前门电磁感应信号 || ctx.R.后门电磁感应信号 || ctx.R.侧门电磁感应信号)
                {
                    return false;
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

        private void SendPulse(Expression<Func<MarkingMachineFeederIOModel, OutputSignal>> outputExpr, int pulseWidthMs = DEFAULT_PULSE_WIDTH_MS, bool now = false)
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

                var ctx = ioManager.Ctx;
                if (ctx == null)
                {
                    _uiLogger.WarnRaw("发送脉冲失败: 未获取到IO上下文实例");
                    return;
                }

                ctx.Pulse(outputExpr, pulseWidthMs, now: now);
                _uiLogger.DebugRaw("已发送脉冲: {0}", outputExpr);
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("发送脉冲异常: {0} - {1}", outputExpr, ex.Message);
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
