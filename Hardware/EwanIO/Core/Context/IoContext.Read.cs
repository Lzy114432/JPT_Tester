using System;
using System.Linq.Expressions;
using EwanIO.Core.Attributes;
using EwanIO.Core.Data;

namespace EwanIO.Core.Context
{
    /// <summary>
    /// IoContext - Read 按索引访问输入/输出
    /// </summary>
    public partial class IoContext<TLayout> where TLayout : class, new()
    {
        #region 按索引访问

        /// <summary>
        /// 按索引读取输入
        /// </summary>
        public bool GetInput(int index)
        {
            if (!EnsureInputIndex(index, nameof(GetInput)))
                return false;
            return _snapshot.Current.GetInput(index);
        }

        /// <summary>
        /// 按索引读取原始输入（经过模拟，未应用 NO/NC 映射）
        /// </summary>
        public bool GetPreMapInput(int index)
        {
            if (!EnsureInputIndex(index, nameof(GetPreMapInput)))
                return false;
            return _snapshot.Current.GetPreMapInput(index);
        }

        /// <summary>
        /// 按索引读取输入（绕过模拟，应用 NO/NC 映射）
        /// </summary>
        public bool GetNoSimInput(int index)
        {
            if (!EnsureInputIndex(index, nameof(GetNoSimInput)))
                return false;
            return _snapshot.Current.GetNoSimInput(index);
        }

        /// <summary>
        /// 按索引读取硬件输入（绕过模拟 + NO/NC 映射）
        /// </summary>
        public bool GetHardwareInput(int index)
        {
            if (!EnsureInputIndex(index, nameof(GetHardwareInput)))
                return false;
            return _snapshot.Current.GetHardwareInput(index);
        }

        /// <summary>
        /// 按表达式读取输入
        /// </summary>
        /// <param name="expr">输入选择表达式</param>
        /// <returns>输入状态</returns>
        public bool GetInput(Expression<Func<TLayout, InputSignal>> expr)
        {
            int index = _meta.GetInputIndex(expr);
            return _snapshot.Current.GetInput(index);
        }

        /// <summary>
        /// 按表达式读取原始输入（经过模拟，未应用 NO/NC 映射）
        /// </summary>
        /// <param name="expr">输入选择表达式</param>
        /// <returns>输入状态</returns>
        public bool GetPreMapInput(Expression<Func<TLayout, InputSignal>> expr)
        {
            int index = _meta.GetInputIndex(expr);
            return _snapshot.Current.GetPreMapInput(index);
        }

        /// <summary>
        /// 按表达式读取输入（绕过模拟，应用 NO/NC 映射）
        /// </summary>
        /// <param name="expr">输入选择表达式</param>
        /// <returns>输入状态</returns>
        public bool GetNoSimInput(Expression<Func<TLayout, InputSignal>> expr)
        {
            int index = _meta.GetInputIndex(expr);
            return _snapshot.Current.GetNoSimInput(index);
        }

        /// <summary>
        /// 按表达式读取硬件输入（绕过模拟 + NO/NC 映射）
        /// </summary>
        /// <param name="expr">输入选择表达式</param>
        /// <returns>输入状态</returns>
        public bool GetHardwareInput(Expression<Func<TLayout, InputSignal>> expr)
        {
            int index = _meta.GetInputIndex(expr);
            return _snapshot.Current.GetHardwareInput(index);
        }

        /// <summary>
        /// 按索引读取输出
        /// </summary>
        public bool GetOutput(int index)
        {
            if (!EnsureOutputIndex(index, nameof(GetOutput)))
                return false;
            return _snapshot.Current.GetOutput(index);
        }

        /// <summary>
        /// 按索引读取输出物理值（应用 NO/NC 映射）
        /// </summary>
        public bool GetPhysicalOutput(int index)
        {
            if (!EnsureOutputIndex(index, nameof(GetPhysicalOutput)))
                return false;
            bool logicalValue = _snapshot.Current.GetOutput(index);
            return _mapping.ApplyOutputLogic(index, logicalValue);
        }

        /// <summary>
        /// 按表达式读取输出
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <returns>输出状态</returns>
        public bool GetOutput(Expression<Func<TLayout, OutputSignal>> expr)
        {
            int index = _meta.GetOutputIndex(expr);
            return _snapshot.Current.GetOutput(index);
        }

        /// <summary>
        /// 按表达式读取输出物理值（应用 NO/NC 映射）
        /// </summary>
        /// <param name="expr">输出选择表达式</param>
        /// <returns>输出物理状态</returns>
        public bool GetPhysicalOutput(Expression<Func<TLayout, OutputSignal>> expr)
        {
            int index = _meta.GetOutputIndex(expr);
            bool logicalValue = _snapshot.Current.GetOutput(index);
            return _mapping.ApplyOutputLogic(index, logicalValue);
        }

        #endregion
    }
}
