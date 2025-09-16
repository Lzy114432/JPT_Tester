using Ewan.Core;
using Ewan.Core.Msg;
using Ewan.Model.System;

namespace Ewan.BusinessBonding
{
    /// <summary>
    /// 系统控制业务服务
    /// 负责系统的启动、停止、状态管理等核心业务逻辑
    /// </summary>
    public class SystemControlService : BaseManager<SystemControlService>
    {


        public void StartSystem()
        {

            Push(SystemControlCommand.Start);
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统启动命令已发送");

        }

        public void StopSystem()
        {

            Push(SystemControlCommand.Stop);
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统停止命令已发送");

        }

        public void EmergencyStopSystem()
        {

            Push(SystemControlCommand.EmergencyStop);
            _uiLogger.Warn(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统紧急停止命令已发送");
        }

        public void PauseSystem()
        {
            Push(SystemControlCommand.Pause);
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统暂停命令已发送");
        }

        public void ResumeSystem()
        {
            Push(SystemControlCommand.Resume);
            _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingCompleted, "系统恢复命令已发送");
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


    }
}