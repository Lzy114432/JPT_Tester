using Ewan.Core.IO;
using Ewan.Core.Module;
using Ewan.Core.ScanCode;
using Ewan.Model.Messages;
using Ewan.Model.Production;
using Ewan.Model.System;
using EwanCommon.Logging;
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using System;

namespace Ewan.Core.Logic
{
    /// <summary>
    /// 物料装载逻辑状态机
    /// 负责将扫码识别后的料片装载到对应的料仓中
    /// 基于 LogicBase + SwitchIndex 模式实现
    /// </summary>
    /// <remarks>
    /// 状态流程：
    /// 初始状态 → 检查前置条件 → 等待料片信号 → 取料中 → 到达扫码位置 → 执行扫码
    /// → 移动到料仓 → 等待装载完成 → 清理状态 → 结束状态
    /// </remarks>
    public class MaterialLoadingLogic : LogicBase
    {
        #region 私有字段

        private readonly UILogger _uiLogger = new UILogger();
        private readonly ProductionLineSharedState _sharedState;
        private readonly LayeredIOManager _ioManager;
        private readonly SystemParametersManager _parametersManager;

        private bool _beltStopRequested = false;
        private int _scanRetryCount = 0;
        private string _scannedCode = string.Empty;
        private int _targetBin = 1;

        // 超时配置（毫秒）
        private const int WAIT_MATERIAL_TIMEOUT = 30000;
        private const int WAIT_SCAN_POSITION_TIMEOUT = 10000;
        private const int WAIT_LOADING_COMPLETE_TIMEOUT = 15000;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sharedState">共享状态对象</param>
        public MaterialLoadingLogic(ProductionLineSharedState sharedState)
        {
            _sharedState = sharedState ?? throw new ArgumentNullException(nameof(sharedState));
            _ioManager = LayeredIOManager.Instance();
            _parametersManager = SystemParametersManager.Instance;
        }

        #endregion

        #region LogicBase 实现

        /// <summary>
        /// 状态机处理器
        /// </summary>
        public override void Handler()
        {
            // 系统暂停时不处理
            if (_sharedState.IsSystemPaused())
            {
                return;
            }

            // 检查模块是否启用
            var parameters = _parametersManager?.Parameters;
            if (parameters != null && !parameters.EnableLoadingModule)
            {
                return;
            }

            switch (SwitchIndex)
            {
                case "初始状态":
                    ProcessInitialState();
                    break;

                case "检查前置条件":
                    ProcessCheckPreconditions();
                    break;

                case "等待料片信号":
                    ProcessWaitForMaterial();
                    break;

                case "取料中":
                    ProcessPickingMaterial();
                    break;

                case "到达扫码位置":
                    ProcessAtScanPosition();
                    break;

                case "执行扫码":
                    ProcessScanning();
                    break;

                case "移动到料仓":
                    ProcessMovingToBin();
                    break;

                case "等待装载完成":
                    ProcessWaitLoadingComplete();
                    break;

                case "清理状态":
                    ProcessCleanup();
                    break;

                case "结束状态":
                    // 完成，等待下一个周期
                    break;
            }
        }

        /// <summary>
        /// 复位状态机
        /// </summary>
        public override void Rset()
        {
            _beltStopRequested = false;
            _scanRetryCount = 0;
            _scannedCode = string.Empty;
            _targetBin = 1;
            base.Rset();
        }

        #endregion

        #region 状态处理方法

        /// <summary>
        /// 处理初始状态
        /// </summary>
        private void ProcessInitialState()
        {
            _uiLogger.DebugRaw("状态机启动: {0}", "MaterialLoadingLogic");
            SwitchIndex = "检查前置条件";
        }

        /// <summary>
        /// 检查前置条件
        /// </summary>
        private void ProcessCheckPreconditions()
        {
            // 检查是否有下料优先级请求
            if (_sharedState.HasUnloadingPriorityRequest())
            {
                _uiLogger.DebugRaw("检测到下料优先级请求，等待下料完成: {0}", "MaterialLoadingLogic");
                // 保持当前状态，等待下料完成
                return;
            }

            // 尝试获取流程锁
            if (!_sharedState.TryStartLoading())
            {
                _uiLogger.DebugRaw("无法获取流程锁，等待中: {0}", "MaterialLoadingLogic");
                return;
            }

            SwitchIndex = "等待料片信号";
            Tw.StartWatch(SwitchIndex);
        }

        /// <summary>
        /// 等待料片信号
        /// </summary>
        private void ProcessWaitForMaterial()
        {
            // 检测料片信号
            if (_ioManager?.Ctx?.R.检测到料片信号 == true)
            {
                // 有料片检测信号，允许取料
                _ioManager.Ctx.On(x => x.触发机械手皮带线允许取料);
                _sharedState.MarkLoadingInProgress();

                _uiLogger.InfoRaw("处理已开始: {0}", "检测到料片信号(X3=true)，允许取料(OUT14=true)，开始装料流程");

                SwitchIndex = "取料中";
                Tw.StartWatch(SwitchIndex);
                return;
            }

            // 超时检查
            if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_MATERIAL_TIMEOUT))
            {
                _uiLogger.WarnRaw("操作超时: {0}", "等待料片信号超时");
                MessageHub.Current.Post(new AlarmMessage(
                    key: "Loading.Timeout",
                    content: "等待料片信号超时",
                    level: AlarmLevel.M,
                    needReset: false,
                    unit: "Loading"));
                ForceCleanup("等待料片超时");
            }
        }

        /// <summary>
        /// 取料中状态
        /// </summary>
        private void ProcessPickingMaterial()
        {
            // 检测机械手忙碌信号，请求停止皮带
            if (!_beltStopRequested && _ioManager?.Ctx?.R.机械手忙碌状态信号 == true)
            {
                _beltStopRequested = true;
                var beltStopMessage = BeltConveyorControlMessage.Stop(
                    BeltConveyorControlSource.MaterialLoading,
                    "机械手正在取料，停止皮带");
                MessageHub.Current.Post(beltStopMessage);
                _uiLogger.InfoRaw("处理已开始: {0}", "检测到机械手忙碌信号，请求停止皮带");
            }

            // 检测是否到达扫码位置
            if (_ioManager?.Ctx?.R.移至扫码区到位信号 == true)
            {
                SwitchIndex = "到达扫码位置";
                _uiLogger.InfoRaw("处理已完成: {0}", "料片已到达扫码位置(X7=true)，开始扫码流程");
                return;
            }

            // 超时检查
            if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_SCAN_POSITION_TIMEOUT))
            {
                _uiLogger.WarnRaw("操作超时: {0}", "等待到达扫码位置超时");
                MessageHub.Current.Post(new AlarmMessage(
                    key: "Loading.Timeout",
                    content: "等待到达扫码位置超时",
                    level: AlarmLevel.M,
                    needReset: false,
                    unit: "Loading"));
                ForceCleanup("取料超时");
            }
        }

        /// <summary>
        /// 到达扫码位置
        /// </summary>
        private void ProcessAtScanPosition()
        {
            // 禁止继续取料
            _ioManager?.Ctx?.Off(x => x.触发机械手皮带线允许取料);

            SwitchIndex = "执行扫码";
            _scanRetryCount = 0;
        }

        /// <summary>
        /// 执行扫码
        /// </summary>
        private void ProcessScanning()
        {
            var parameters = _parametersManager?.Parameters;
            bool mesEnabled = parameters?.MesEnabled ?? false;
            int maxRetry = parameters?.CodeReaderScanRetryCount ?? 3;
            if (maxRetry <= 0) maxRetry = 3;

            if (mesEnabled)
            {
                _scanRetryCount++;
                _scannedCode = (ScannerManager.Instance().TriggerScan() ?? string.Empty).Trim();

                if (!string.IsNullOrWhiteSpace(_scannedCode))
                {
                    _uiLogger.InfoRaw("处理已完成: {0}", $"扫码内容: {_scannedCode}");
                }
                else if (_scanRetryCount < maxRetry)
                {
                    _uiLogger.WarnRaw("操作失败: {0}", $"第{_scanRetryCount}次扫码无结果，重试中");
                    return;
                }
                else
                {
                    _uiLogger.WarnRaw("操作失败: {0}", $"连续扫码{maxRetry}次无结果，继续流程");
                    MessageHub.Current.Post(new AlarmMessage(
                        key: "Scan.Failed",
                        content: $"装料扫码失败：连续扫码{maxRetry}次无结果",
                        level: AlarmLevel.L,
                        needReset: false,
                        unit: "Scanner"));
                }
            }
            else
            {
                _uiLogger.InfoRaw("处理已跳过: {0}", "MES未启用，跳过扫码");
            }

            // 发送扫码完成信号
            _ioManager?.Ctx?.On(x => x.发送扫码完成信号);

            // 获取目标料仓
            _targetBin = GetConfiguredBinNumber();
            SetBinSelectSignal(_targetBin);

            SwitchIndex = "移动到料仓";
        }

        /// <summary>
        /// 移动到料仓
        /// </summary>
        private void ProcessMovingToBin()
        {
            // 触发放入料仓信号
            _ioManager?.Ctx?.On(x => x.触发机械手放置料仓);

            _uiLogger.InfoRaw("处理已开始: {0}", $"开始移动到料仓{_targetBin}位置");

            SwitchIndex = "等待装载完成";
            Tw.StartWatch(SwitchIndex);
        }

        /// <summary>
        /// 等待装载完成
        /// </summary>
        private void ProcessWaitLoadingComplete()
        {
            // 检查装载完成状态
            if (_sharedState.GetLoadingCompleted())
            {
                SwitchIndex = "清理状态";
                return;
            }

            // 超时检查
            if (Tw.StartCheckIsTimeout(SwitchIndex, WAIT_LOADING_COMPLETE_TIMEOUT))
            {
                _uiLogger.WarnRaw("操作超时: {0}", "等待装载完成超时");
                MessageHub.Current.Post(new AlarmMessage(
                    key: "Loading.Timeout",
                    content: "等待装载完成超时",
                    level: AlarmLevel.M,
                    needReset: false,
                    unit: "Loading"));
                ForceCleanup("装载超时");
            }
        }

        /// <summary>
        /// 清理状态
        /// </summary>
        private void ProcessCleanup()
        {
            // 清除装料信号
            _ioManager?.Ctx?.Off(x => x.触发机械手放置料仓);
            ClearBinSelectSignals();
            _ioManager?.Ctx?.Off(x => x.发送扫码完成信号);

            // 清除SharedState标志
            _sharedState.ClearLoadingInProgress();
            _sharedState.SetLoadingCompleted(false);
            _sharedState.FinishProcess();

            // 释放皮带控制
            if (_beltStopRequested)
            {
                _beltStopRequested = false;
                var beltReleaseMessage = BeltConveyorControlMessage.Release(
                    BeltConveyorControlSource.MaterialLoading,
                    "装料完成，释放皮带");
                MessageHub.Current.Post(beltReleaseMessage);
            }

            _uiLogger.InfoRaw("处理已完成: {0}", "装料完成，清理状态，回到初始");

            Complete();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 强制清理并返回初始状态
        /// </summary>
        public void ForceCleanup(string reason)
        {
            _uiLogger.WarnRaw("强制清理: {0}", reason);

            try
            {
                if (_ioManager?.Ctx != null)
                {
                    _ioManager.Ctx.Off(x => x.触发机械手皮带线允许取料);
                    _ioManager.Ctx.Off(x => x.发送取料指令);
                    _ioManager.Ctx.Off(x => x.触发机械手放置料仓);
                    _ioManager.Ctx.Off(x => x.发送扫码完成信号);
                    ClearBinSelectSignals();
                }
            }
            catch (Exception ex)
            {
                _uiLogger.ErrorRaw("处理错误: {0} - {1}", "强制清理IO", ex.Message);
            }

            _sharedState.ClearLoadingInProgress();
            _sharedState.SetLoadingCompleted(false);
            _sharedState.FinishProcess();

            if (_beltStopRequested)
            {
                _beltStopRequested = false;
                var message = BeltConveyorControlMessage.Release(
                    BeltConveyorControlSource.MaterialLoading,
                    reason);
                MessageHub.Current.Post(message);
            }

            Rset();
        }

        /// <summary>
        /// 获取配置的料仓编号
        /// </summary>
        private int GetConfiguredBinNumber()
        {
            var selected = _parametersManager?.Parameters?.LoadingBinSelection ?? BinSelection.Bin1;
            switch (selected)
            {
                case BinSelection.Bin2: return 2;
                case BinSelection.Bin3: return 3;
                default: return 1;
            }
        }

        /// <summary>
        /// 设置料仓选择信号
        /// </summary>
        private void SetBinSelectSignal(int binNumber)
        {
            if (_ioManager?.Ctx == null) return;

            ClearBinSelectSignals();

            switch (binNumber)
            {
                case 1:
                    _ioManager.Ctx.On(x => x.料仓1选择信号);
                    break;
                case 2:
                    _ioManager.Ctx.On(x => x.料仓2选择信号);
                    break;
                case 3:
                    _ioManager.Ctx.On(x => x.料仓3选择信号);
                    break;
            }
        }

        /// <summary>
        /// 清除所有料仓选择信号
        /// </summary>
        private void ClearBinSelectSignals()
        {
            if (_ioManager?.Ctx == null) return;

            _ioManager.Ctx.Off(x => x.料仓1选择信号);
            _ioManager.Ctx.Off(x => x.料仓2选择信号);
            _ioManager.Ctx.Off(x => x.料仓3选择信号);
        }

        #endregion
    }
}
