using System;
using System.Collections.Generic;

namespace EwanCore.Plc.Cmd
{
    /// <summary>
    /// 双端队列（带容量限制）。
    /// </summary>
    public class Deque<T>
    {
        private readonly LinkedList<T> _list = new LinkedList<T>();
        private readonly int _capacity;

        /// <summary>
        /// 创建一个无容量限制的双端队列。
        /// </summary>
        public Deque()
        {
        }

        /// <summary>
        /// 创建一个带容量限制的双端队列。
        /// 超出容量时会自动丢弃一端的数据（入队方向的反方向）。
        /// </summary>
        /// <param name="capacity">容量；小于等于 0 表示不限制。</param>
        public Deque(int capacity)
        {
            _capacity = capacity;
        }

        /// <summary>
        /// 从队头入队。
        /// </summary>
        /// <param name="value">元素。</param>
        public void EnqueueFirst(T value)
        {
            if (_capacity > 0 && _list.Count == _capacity)
            {
                _list.RemoveLast();
            }
            _list.AddFirst(value);
        }

        /// <summary>
        /// 从队尾入队。
        /// </summary>
        /// <param name="value">元素。</param>
        public void EnqueueLast(T value)
        {
            if (_capacity > 0 && _list.Count == _capacity)
            {
                _list.RemoveFirst();
            }
            _list.AddLast(value);
        }

        /// <summary>
        /// 从队头出队。
        /// </summary>
        /// <returns>元素。</returns>
        public T DequeueFirst()
        {
            if (_list.Count == 0)
            {
                throw new InvalidOperationException("Deque is empty");
            }

            var value = _list.First.Value;
            _list.RemoveFirst();
            return value;
        }

        /// <summary>
        /// 从队尾出队。
        /// </summary>
        /// <returns>元素。</returns>
        public T DequeueLast()
        {
            if (_list.Count == 0)
            {
                throw new InvalidOperationException("Deque is empty");
            }

            var value = _list.Last.Value;
            _list.RemoveLast();
            return value;
        }
    }
}
