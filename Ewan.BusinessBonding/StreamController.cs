using Ewan.Core;
using Ewan.Core.Attribute;
using Ewan.Core.Module.Interface;
using Ewan.Core.Run;
using System;
using System.Collections.Generic;

namespace Ewan.BusinessBonding
{
    /// <summary>
    /// 流程运行控制器
    /// </summary>
    [Manager(Priority = 3)]
    public class StreamController : BaseManager<StreamController>
    {
        #region 流程runner

        /// <summary>
        /// 主流程runner
        /// </summary>
        private StreamRunner _mainRunner;

        /// <summary>
        /// PLC心跳流程runner
        /// </summary>
        private StreamRunner _plcHeartRunner;

        #endregion

        #region 节点集合

        private List<IModule> _mainModules = new List<IModule>();
        private List<IModule> _plcHeartModules = new List<IModule>();


        #endregion

        public override bool Init()
        {
            #region  //构造主流程的节点并加入到对应runner
            //_mainModules.Add(new PlcModule());//测试可以换成数据模拟节点 根据配置决定加载哪个PLC节点
            //_mainModules.Add(new AlarmModule<PlcModel>());
            //_mainRunner = new StreamRunner(_mainModules);
            #endregion

            #region //构造plc心跳流程的节点并加入到对应runner

            //_plcHeartModules.Add(new PlcHeartModule());
            //_plcHeartRunner = new StreamRunner(_plcHeartModules);

            #endregion


            return base.Init();
        }
        /// <summary>
        /// 开启运行.
        /// </summary>
        public void StartRun()
        {
            try
            {
                //1.运行主流程
                StartMainStream();
                //2.运行plc心跳流程
                StartPlcHeartStream();
                ////3.运行plc产能统计流程
                //StartPlcProductCapacityStream();

                ////...n.运行其他流程
                //StartOtherStream();
            }
            catch (Exception ex)
            {
                ////s_logger.Error($"StartRun occur exception:{ex}");
                ////停止流程
                //StopMainStream();
                //StopPlcHeartStream();
                //StopPlcProductCapacityStream();
                //StopOtherStream();
            }
        }

        ///// <summary>
        /// 停止运行
        /// </summary>
        public void StopRun()
        {
            //1.停止主流程
            StopMainStream();
            //2.停止plc心跳流程
            StopPlcHeartStream();

            ////...n.停止其他流程
            //StopOtherStream();
        }

        /// <summary>
        /// 启动主流程
        /// </summary>
        private void StartMainStream()
        {
            if (_mainRunner != null)
            {
                _mainRunner.Start();
            }
        }


        /// <summary>
        /// 启动心跳流程
        /// </summary>
        private void StartPlcHeartStream()
        {
            if (_plcHeartRunner != null)
            {
                _plcHeartRunner.Start();
            }
        }

        ///// <summary>
        ///// 启动产能流程
        ///// </summary>
        //private void StartPlcProductCapacityStream()
        //{
        //    if (_plcProductCapacityRunner != null)
        //    {
        //        _plcProductCapacityRunner.Start();
        //    }
        //}

        /// <summary>
        /// 停止主流程
        /// </summary>
        private void StopMainStream()
        {
            _mainRunner.Stop();
        }

        /// <summary>
        /// 停止心跳流程
        /// </summary>
        private void StopPlcHeartStream()
        {
            _plcHeartRunner.Stop();
        }

        ///// <summary>
        ///// 停止心跳流程
        ///// </summary>
        //private void StopPlcProductCapacityStream()
        //{
        //    _plcProductCapacityRunner.Stop();
        //}

        ///// <summary>
        ///// 启动其他流程
        ///// </summary>
        //private void StartOtherStream()
        //{
        //    _otherRunner.Start();
        //}

        ///// <summary>
        ///// 停止主流程
        ///// </summary>
        //private void StopOtherStream()
        //{
        //    _otherRunner.Stop();
        //}


    }
}
