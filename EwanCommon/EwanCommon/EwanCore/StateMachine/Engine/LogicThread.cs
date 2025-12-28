using System;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 兼容命名：LogicThread（参考 Byron.Commond.Logic.LogicThread / ScribingV3）
    /// 在 EwanCommon 中复用 <see cref="LogicRunner"/> 实现。
    /// </summary>
    public sealed class LogicThread : IDisposable
    {
        private readonly LogicRunner _runner;

        public LogicThread()
        {
            _runner = new LogicRunner();
        }

        /// <summary>
        /// 队列中的逻辑数量。
        /// </summary>
        public int Count => _runner.Count;

        /// <summary>
        /// 运行标记。
        /// </summary>
        public RunTimeTag RunTag => _runner.RunTag;

        /// <summary>
        /// 当前状态字符串（用于监控显示）。
        /// </summary>
        public string CurLogicStateStr => _runner.CurLogicStateStr;

        /// <summary>
        /// 逻辑执行异常（发生异常时会自动切到 Stop，避免线程直接崩溃）。
        /// </summary>
        public event EventHandler<LogicExceptionEventArgs> LogicException
        {
            add => _runner.LogicException += value;
            remove => _runner.LogicException -= value;
        }

        /// <summary>
        /// 追加一个逻辑到队列。
        /// </summary>
        public void AddAction(LogicBase logic) => _runner.AddAction(logic);

        /// <summary>
        /// 清空队列（仅建议在 Stop/Pause 状态下调用）。
        /// </summary>
        public void ClearAction() => _runner.ClearAction();

        /// <summary>
        /// 队列中是否存在指定类型的逻辑。
        /// </summary>
        public bool ExistAction(Type baseLogicType) => _runner.ExistAction(baseLogicType);

        /// <summary>
        /// 启动：进入 Run 模式。
        /// </summary>
        public void Start() => _runner.Start();

        /// <summary>
        /// 停止：进入 Stop 模式（不会清空队列）。
        /// </summary>
        public void Stop() => _runner.Stop();

        /// <summary>
        /// 暂停：进入 Pause 模式（不会清空队列）。
        /// </summary>
        public void Pause() => _runner.Pause();

        /// <summary>
        /// 单步：执行一轮后自动切到 Stop。
        /// </summary>
        public void Step() => _runner.Step();

        public void Dispose() => _runner.Dispose();
    }
}

