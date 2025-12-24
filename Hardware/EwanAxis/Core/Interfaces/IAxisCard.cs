/*===================================================
 * 类名称: IAxisCard
 * 类描述: 轴卡接口（与IO系统完全解耦）
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

using System;
using System.Collections.Generic;

namespace EwanAxis.Core.Interfaces
{
    /// <summary>
    /// 轴卡接口
    /// 注意：此接口不包含任何通用IO操作方法
    /// 通用IO由独立的 EwanIO 系统管理
    /// </summary>
    public interface IAxisCard : IDisposable
    {
        /// <summary>
        /// 卡名称
        /// </summary>
        string CardName { get; set; }

        /// <summary>
        /// 卡号
        /// </summary>
        int CardIndex { get; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 轴数量
        /// </summary>
        int AxisCount { get; }

        /// <summary>
        /// 轴集合（只读）
        /// </summary>
        IReadOnlyList<IAxis> Axes { get; }

        /// <summary>
        /// 通过索引获取轴
        /// </summary>
        /// <param name="index">轴索引</param>
        /// <returns>轴对象</returns>
        IAxis this[int index] { get; }

        /// <summary>
        /// 通过名称获取轴
        /// </summary>
        /// <param name="name">轴名称</param>
        /// <returns>轴对象，如果未找到返回null</returns>
        IAxis? GetAxisByName(string name);

        #region 生命周期

        /// <summary>
        /// 初始化
        /// 加载配置文件等
        /// </summary>
        /// <param name="configPath">配置文件路径</param>
        /// <returns>是否成功</returns>
        bool Initialize(string configPath);

        /// <summary>
        /// 连接硬件
        /// </summary>
        /// <returns>是否成功</returns>
        bool Connect();

        /// <summary>
        /// 断开连接
        /// </summary>
        /// <returns>是否成功</returns>
        bool Disconnect();

        /// <summary>
        /// 保存配置
        /// </summary>
        /// <param name="configPath">配置文件路径（为空则使用初始化时的路径）</param>
        /// <returns>是否成功</returns>
        bool SaveConfig(string? configPath = null);

        #endregion

        #region 全局操作

        /// <summary>
        /// 全部轴紧急停止
        /// </summary>
        void EmgStopAll();

        /// <summary>
        /// 全部轴励磁
        /// </summary>
        /// <param name="enable">true=励磁，false=释放</param>
        void ServoOnAll(bool enable);

        /// <summary>
        /// 清除所有轴报警
        /// </summary>
        void ClearAllErrors();

        #endregion

        #region 事件

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        event EventHandler<ConnectionChangedEventArgs>? ConnectionChanged;

        /// <summary>
        /// 轴报警事件
        /// </summary>
        event EventHandler<AxisAlarmEventArgs>? AxisAlarm;

        #endregion
    }

    /// <summary>
    /// 连接状态变化事件参数
    /// </summary>
    public class ConnectionChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string? Message { get; }

        public ConnectionChangedEventArgs(bool isConnected, string? message = null)
        {
            IsConnected = isConnected;
            Message = message;
        }
    }

    /// <summary>
    /// 轴报警事件参数
    /// </summary>
    public class AxisAlarmEventArgs : EventArgs
    {
        public int AxisIndex { get; }
        public string AxisName { get; }
        public string AlarmMessage { get; }

        public AxisAlarmEventArgs(int axisIndex, string axisName, string alarmMessage)
        {
            AxisIndex = axisIndex;
            AxisName = axisName;
            AlarmMessage = alarmMessage;
        }
    }
}
