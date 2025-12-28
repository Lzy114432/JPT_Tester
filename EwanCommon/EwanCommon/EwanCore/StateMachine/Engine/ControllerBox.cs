using System;
using System.Collections.Generic;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 兼容命名：ControllerBox（参考 Byron.Commond.ControllerBox / ScribingV3）
    /// 统一控制多个 <see cref="LogicThread"/> 的启停/单步。
    /// </summary>
    public sealed class ControllerBox
    {
        private readonly List<LogicThread> _threads = new List<LogicThread>();

        /// <summary>
        /// 添加一个逻辑线程到控制器。
        /// </summary>
        public void AddLogicManger(LogicThread logicThread)
        {
            if (logicThread == null) throw new ArgumentNullException(nameof(logicThread));
            _threads.Add(logicThread);
        }

        /// <summary>
        /// 启动所有逻辑线程（Run 模式）。
        /// </summary>
        public void Start()
        {
            for (var i = 0; i < _threads.Count; i++)
            {
                _threads[i].Start();
            }
        }

        /// <summary>
        /// 停止所有逻辑线程（Stop 模式）。
        /// </summary>
        public void Stop()
        {
            for (var i = 0; i < _threads.Count; i++)
            {
                _threads[i].Stop();
            }
        }

        /// <summary>
        /// 暂停所有逻辑线程（Pause 模式）。
        /// </summary>
        public void Pause()
        {
            for (var i = 0; i < _threads.Count; i++)
            {
                _threads[i].Pause();
            }
        }

        /// <summary>
        /// 单步执行所有逻辑线程（Step 模式）。
        /// </summary>
        public void Step()
        {
            for (var i = 0; i < _threads.Count; i++)
            {
                _threads[i].Step();
            }
        }
    }
}

