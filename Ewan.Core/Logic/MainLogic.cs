using Ewan.Model.System;
using EwanCore.StateMachine;
using System;

namespace Ewan.Core.Logic
{
    /// <summary>
    /// 主逻辑：在一个 LogicBase 中循环驱动装料/下料子逻辑（参考 ScribingV3 MainLogic 模式）。
    /// </summary>
    public class MainLogic : LogicBase, IDisposable
    {
        private readonly SystemParametersManager _parametersManager;
        private readonly LogicBase _loadingLogic;
        private readonly LogicBase _unloadingLogic;

        private bool _loadingEnabled = true;
        private bool _unloadingEnabled = true;

        public MainLogic()
        {
            _parametersManager = SystemParametersManager.Instance;

            var loadingLogic = new MaterialLoadingLogic();
            var unloadingLogic = new MaterialUnloadingLogic();

            _loadingLogic = loadingLogic;
            _unloadingLogic = unloadingLogic;
        }

        /// <summary>
        /// 仅用于单元测试：注入子逻辑，避免硬件依赖。
        /// </summary>
        internal MainLogic(LogicBase loadingLogic, LogicBase unloadingLogic)
        {
            _parametersManager = SystemParametersManager.Instance;
            _loadingLogic = loadingLogic ?? throw new ArgumentNullException(nameof(loadingLogic));
            _unloadingLogic = unloadingLogic ?? throw new ArgumentNullException(nameof(unloadingLogic));
        }

        public override void Handler()
        {
            switch (SwitchIndex)
            {
                case "初始状态":
                    RefreshModuleConfiguration();
                    _loadingLogic.Rset();
                    _unloadingLogic.Rset();
                    SwitchIndex = "装料流程";
                    break;

                case "装料流程":
                    RefreshModuleConfiguration();

                    if (_loadingEnabled)
                    {
                        _loadingLogic.Handler();
                        if (_loadingLogic.IsFinish)
                        {
                            _loadingLogic.Rset();
                            SwitchIndex = "下料流程";
                        }
                    }
                    else
                    {
                        SwitchIndex = "下料流程";
                    }
                    break;

                case "下料流程":
                    RefreshModuleConfiguration();

                    if (_unloadingEnabled)
                    {
                        _unloadingLogic.Handler();
                        if (_unloadingLogic.IsFinish)
                        {
                            _unloadingLogic.Rset();
                            SwitchIndex = "装料流程";
                        }
                    }
                    else
                    {
                        SwitchIndex = "装料流程";
                    }
                    break;

                case "结束状态":
                    Complete();
                    break;
            }
        }

        private void RefreshModuleConfiguration()
        {
            var parameters = _parametersManager?.Parameters;
            if (parameters == null)
            {
                return;
            }

            _loadingEnabled = parameters.EnableLoadingModule;
            _unloadingEnabled = parameters.EnableUnloadingModule;
        }

        /// <summary>
        /// 重写步骤切换回调，禁用日志输出以避免空闲时刷屏
        /// </summary>
        protected override void OnStepChanged(string from, string to)
        {
            // 只保留 TimeoutWatch 功能，不输出日志
            Tw.StopWatch(from);
            Tw.StopWatch(to);
            Tw.StartWatch(to);
        }

        public void Dispose()
        {
            if (_unloadingLogic is MaterialUnloadingLogic unloadingLogic)
            {
                unloadingLogic.Dispose();
            }
        }
    }
}
