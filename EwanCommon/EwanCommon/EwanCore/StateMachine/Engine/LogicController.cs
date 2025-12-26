using System;
using System.Collections.Generic;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 逻辑控制器（参考 Byron.Commond.Logic.ControllerBox）：统一控制多个 <see cref="LogicRunner"/>。
    /// </summary>
    public sealed class LogicController
    {
        private readonly List<LogicRunner> _runners = new List<LogicRunner>();

        /// <summary>
        /// 创建控制器并可选传入一组 runner。
        /// </summary>
        /// <param name="runners">要统一控制的 runner。</param>
        public LogicController(params LogicRunner[] runners)
        {
            if (runners == null)
            {
                return;
            }

            for (var i = 0; i < runners.Length; i++)
            {
                AddRunner(runners[i]);
            }
        }

        /// <summary>
        /// 添加一个 runner。
        /// </summary>
        /// <param name="runner">runner 实例。</param>
        public void AddRunner(LogicRunner runner)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            _runners.Add(runner);
        }

        /// <summary>
        /// 启动所有 runner（Run 模式）。
        /// </summary>
        public void Start()
        {
            for (var i = 0; i < _runners.Count; i++)
            {
                _runners[i].Start();
            }
        }

        /// <summary>
        /// 停止所有 runner（Stop 模式）。
        /// </summary>
        public void Stop()
        {
            for (var i = 0; i < _runners.Count; i++)
            {
                _runners[i].Stop();
            }
        }

        /// <summary>
        /// 暂停所有 runner（Pause 模式）。
        /// </summary>
        public void Pause()
        {
            for (var i = 0; i < _runners.Count; i++)
            {
                _runners[i].Pause();
            }
        }

        /// <summary>
        /// 单步执行所有 runner（Step 模式）。
        /// </summary>
        public void Step()
        {
            for (var i = 0; i < _runners.Count; i++)
            {
                _runners[i].Step();
            }
        }
    }
}
