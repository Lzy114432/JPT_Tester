using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EwanCore.Utils
{
    /// <summary>
    /// 记录一个值的“上一次/当前”变化，用于判断是否发生变化及变化方向。
    /// </summary>
    /// <typeparam name="T">可比较的值类型。</typeparam>
    public class InstanceChange<T> where T : IComparable<T>
    {
        private bool _Inited = false;

        private T _LastValue = default(T);

        private T _CurrentValue = default(T);

        /// <summary>
        /// 是否已初始化（是否调用过 <see cref="Init"/>）。
        /// </summary>
        public bool Inited()
        {
            return _Inited;
        }

        /// <summary>
        /// 初始化当前值。
        /// </summary>
        /// <param name="initVal">初始值。</param>
        public void Init(T initVal)
        {
            SetCurrentValue(initVal);
            _Inited = true;
        }

        /// <summary>
        /// 设置当前值，并将原当前值保存到“上一次值”。
        /// </summary>
        /// <param name="currentVal">当前值。</param>
        public void SetCurrentValue(T currentVal)
        {
            _LastValue = _CurrentValue;
            _CurrentValue = currentVal;
        }

        /// <summary>
        /// 是否发生变化，并返回变化方向。
        /// </summary>
        /// <param name="toBigOrToSmall">true 表示变大；false 表示变小。</param>
        /// <returns>是否发生变化。</returns>
        public bool HasChange(out bool toBigOrToSmall)
        {
            toBigOrToSmall = false;
            var compare = _CurrentValue.CompareTo(_LastValue);
            if (compare == 0)
            {
                return false;
            }

            toBigOrToSmall = compare > 0;
            return true;

        }

    }
}
