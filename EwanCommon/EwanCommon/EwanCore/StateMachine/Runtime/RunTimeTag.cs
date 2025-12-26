namespace EwanCore.StateMachine
{
    /// <summary>
    /// 运行标记（参考 Byron.Commond.Logic.RunTimeTag）。
    /// </summary>
    public enum RunTimeTag
    {
        /// <summary>
        /// 运行。
        /// </summary>
        Run = 0,

        /// <summary>
        /// 单步：执行一轮后自动切换为 <see cref="Stop"/>。
        /// </summary>
        Step = 1,

        /// <summary>
        /// 停止/暂停：不执行逻辑，仅保持线程存活。
        /// </summary>
        Stop = 2,

        /// <summary>
        /// 显式暂停：语义上等价于 <see cref="Stop"/>，用于区分 UI 的“暂停/停止”按钮。
        /// </summary>
        Pause = 3
    }
}

