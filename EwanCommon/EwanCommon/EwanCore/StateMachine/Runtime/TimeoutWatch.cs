using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace EwanCore.StateMachine
{
    /// <summary>
    /// 超时监视（参考 Byron.Commond.TimeoutWatch）：用 name 作为 key 记录计时器。
    /// </summary>
    public sealed class TimeoutWatch : IEnumerable<KeyValuePair<string, TimeSpan>>
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, Stopwatch> _watches = new Dictionary<string, Stopwatch>(StringComparer.Ordinal);

        /// <summary>
        /// 开始计时（如果已存在则保持原计时不重置）。
        /// </summary>
        public void StartWatch(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_watches.TryGetValue(name, out var sw))
                {
                    if (!sw.IsRunning)
                    {
                        sw.Start();
                    }
                    return;
                }

                sw = new Stopwatch();
                sw.Start();
                _watches[name] = sw;
            }
        }

        /// <summary>
        /// 检查是否超时：若 name 首次出现会自动开始计时并返回 false。
        /// </summary>
        public bool StartCheckIsTimeout(string watchName, int timeoutMs)
        {
            if (string.IsNullOrWhiteSpace(watchName) || timeoutMs <= 0)
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (!_watches.TryGetValue(watchName, out var sw))
                {
                    sw = new Stopwatch();
                    sw.Start();
                    _watches[watchName] = sw;
                    return false;
                }

                return sw.ElapsedMilliseconds > timeoutMs;
            }
        }

        /// <summary>
        /// 停止并移除计时。
        /// </summary>
        public void StopWatch(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_watches.TryGetValue(name, out var sw))
                {
                    sw.Stop();
                    _watches.Remove(name);
                }
            }
        }

        /// <summary>
        /// 停止并移除所有计时。
        /// </summary>
        public void StopAllWatch()
        {
            lock (_syncRoot)
            {
                foreach (var sw in _watches.Values)
                {
                    sw.Stop();
                }
                _watches.Clear();
            }
        }

        /// <summary>
        /// 是否存在 name 的计时器。
        /// </summary>
        public bool IsExit(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            lock (_syncRoot)
            {
                return _watches.ContainsKey(name);
            }
        }

        /// <summary>
        /// 获取指定 name 的耗时（毫秒）。
        /// </summary>
        public double GetTimeSpan(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 0;
            }

            lock (_syncRoot)
            {
                return _watches.TryGetValue(name, out var sw) ? sw.Elapsed.TotalMilliseconds : 0;
            }
        }

        public IEnumerator<KeyValuePair<string, TimeSpan>> GetEnumerator()
        {
            KeyValuePair<string, TimeSpan>[] snapshot;
            lock (_syncRoot)
            {
                var list = new List<KeyValuePair<string, TimeSpan>>(_watches.Count);
                foreach (var kv in _watches)
                {
                    list.Add(new KeyValuePair<string, TimeSpan>(kv.Key, kv.Value.Elapsed));
                }
                snapshot = list.ToArray();
            }

            for (var i = 0; i < snapshot.Length; i++)
            {
                yield return snapshot[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

