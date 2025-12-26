using EwanCore.Module.Interface;
using log4net;
using EwanCommon.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EwanCore.Module
{
    /// <summary>
    /// 流程节点基类。
    /// </summary>
    /// <typeparam name="M">用于日志分类的类型（通常传入派生类自身）。</typeparam>
    public abstract class BaseModule<M> : IModule
    {
        protected static readonly ILog s_logger = Log.GetLogger(typeof(M));

        /// <summary>
        /// 一般用于存放当前节点的结果数据。
        /// </summary>
        protected object Data;

        /// <inheritdoc />
        public void Init()
        {
            OnInit();
        }

        /// <inheritdoc />
        public bool Run()
        {
            return OnRun();
        }

        /// <inheritdoc />
        public void SetObject(object obj)
        {
            Data = obj;
        }

        /// <inheritdoc />
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
