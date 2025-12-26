using EwanCore.Module.Interface;
using log4net;
using EwanCommon.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EwanCore.Runner
{
    /// <summary>
    /// 流程运行器：按顺序循环执行一组 <see cref="IModule"/>。
    /// </summary>
    public class StreamRunner
    {
        private readonly ILog _logger = Log.GetLogger(typeof(StreamRunner));

        /// <summary>
        /// 当前runner所要运行的节点
        /// </summary>
        private readonly List<IModule> _modules = new List<IModule>();

        private volatile bool _isAlive = false;

        /// <summary>
        /// 创建一个流程运行器。
        /// </summary>
        /// <param name="modules">要运行的模块列表（按顺序执行）。</param>
        public StreamRunner(List<IModule> modules)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
        }

        /// <summary>
        /// 初始化模块并启动后台循环。
        /// </summary>
        public void Start()
        {
            InitStream();
            _logger.Info($"Init all modules succeed in current stream");

            //确保前面代码都无问题才开启
            _isAlive = true;

            //开启子线程运行各节点
            Task.Run(ExecuteStream);
        }

        /// <summary>
        /// 初始化各个节点
        /// </summary>
        private void InitStream()
        {
            foreach (var module in _modules)
            {
                module.Init();
            }
        }


        private void ExecuteStream()
        {
            //return;
            while (_isAlive)
            {
                Thread.Sleep(1);
                foreach (var module in _modules)
                {
                    if (!_isAlive)
                    {
                        break;
                    }
                    if (!module.Run())//如果一个节点失败,则跳出本次流程,后面节点不必再执行.
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 停止后台循环并销毁所有模块（按顺序）。
        /// </summary>
        public void Stop()
        {
            _isAlive = false;
            foreach (var module in _modules)
            {
                module.Destroy();
            }
        }
    }
}
