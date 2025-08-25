using Ewan.Core.Module.Interface;
using Ewan.Core.Logger;

namespace Ewan.Core.Module
{
    public abstract class BaseModule<M> : IModule
    {
        protected readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));

        /// <summary>
        /// 一般用于存放当前节点的结果数据
        /// </summary>
        protected object Data;

        public void Init()
        {
            OnInit();
        }

        public bool Run()
        {
            return OnRun();
        }

        public void SetObject(object obj)
        {
            Data = obj;
        }

        public void Destroy()
        {
            OnDestroy();
        }

        #region 抽象方法

        protected abstract void OnInit();

        protected abstract bool OnRun();

        protected abstract void OnDestroy();


        #endregion

    }
}
