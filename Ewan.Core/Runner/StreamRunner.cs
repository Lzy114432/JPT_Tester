using Ewan.Core.Module.Interface;
using Ewan.Core.Logger;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ewan.Core.Run
{
    public class StreamRunner
    {
        private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));

        /// <summary>
        /// </summary>
        /// 当前runner所要运行的节点
        private List<IModule> _modules = new List<IModule>();

        private volatile bool _isAlive = false;

        public StreamRunner(List<IModule> modules)
        {
            _modules = modules;
        }

        public void Start()
        {
            InitStream();
            _uiLogger.Info("流运行器模块初始化完成: {0}");
            //开启子线程运行各节点
            Task.Run(() =>
            {
                ExecuteStream();
            });

            //确保前面代码都无问题才开启
            _isAlive = true;
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
