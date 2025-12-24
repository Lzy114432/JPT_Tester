/*===================================================
 * 类名称: SMC606Card
 * 类描述: 雷赛SMC606运动控制卡实现
 * 创建人: Ewan
 * 创建时间: 2025-12-21
 * 版本: V1.0
 =====================================================*/

using System;
using EwanAxis.Core.Interfaces;
using EwanSMC606;

namespace EwanAxis.Hardware.SMC606
{
    /// <summary>
    /// 雷赛SMC606运动控制卡
    /// </summary>
    public class SMC606Card : AxisCardBase
    {
        private ushort _cardNo;
        private string _ipAddress = "";
        private Smc606Lease? _lease;
        private readonly object _fallbackSyncRoot = new object();

        internal object SyncRoot => _lease?.SyncRoot ?? _fallbackSyncRoot;

        /// <summary>
        /// 卡号
        /// </summary>
        public ushort CardNo
        {
            get => _cardNo;
            set => _cardNo = value;
        }

        /// <summary>
        /// 控制卡IP地址（网络连接模式）
        /// </summary>
        public string IpAddress
        {
            get => _ipAddress;
            set => _ipAddress = value ?? "";
        }

        /// <summary>
        /// 连接类型：0=PCI, 1=PCI-E, 2=Ethernet
        /// </summary>
        public ushort ConnectType { get; set; } = 2;

        /// <summary>
        /// 通信波特率
        /// </summary>
        public uint BaudRate { get; set; } = 1000000;

        public override int CardIndex => _cardNo;

        public SMC606Card()
        {
        }

        public SMC606Card(ushort cardNo, string ipAddress)
        {
            _cardNo = cardNo;
            _ipAddress = ipAddress;
        }

        #region 生命周期

        public override bool Initialize(string configPath)
        {
            if (!base.Initialize(configPath))
                return false;

            // 如果没有从配置加载轴，创建默认配置
            if (_axes.Count == 0)
            {
                CreateDefaultConfig();
                // 避免“空配置文件”导致每次启动都走默认逻辑：默认轴生成后写回配置文件。
                SaveConfig();
            }

            return true;
        }

        public override bool Connect()
        {
            if (IsConnected) return true;

            try
            {
                var options = new Smc606ConnectionOptions
                {
                    CardNo = _cardNo,
                    ConnectType = ConnectType,
                    ConnectString = _ipAddress,
                    BaudRate = BaudRate
                };

                _lease = Smc606ConnectionPool.Acquire(options);
                IsConnected = true;
                OnConnectionChanged(true, $"SMC606 Card {_cardNo} connected successfully");
                return true;
            }
            catch (Exception ex)
            {
                OnConnectionChanged(false, $"SMC606 Card {_cardNo} connection exception: {ex.Message}");
                _lease?.Dispose();
                _lease = null;
                return false;
            }
        }

        public override bool Disconnect()
        {
            if (!IsConnected) return true;

            try
            {
                // 先停止所有轴
                EmgStopAll();
                _lease?.Dispose();
                _lease = null;
                IsConnected = false;
                OnConnectionChanged(false, "Disconnected");
                return true;
            }
            catch
            {
                try
                {
                    _lease?.Dispose();
                }
                catch
                {
                    // ignored
                }
                _lease = null;
                IsConnected = false;
                return false;
            }
        }

        protected override IAxis CreateAxis(AxisParameter parameter)
        {
            return new SMC606Axis(this, parameter);
        }

        protected override void CreateDefaultConfig()
        {
            // 默认创建6个轴（SMC606支持6轴）
            for (int i = 0; i < 6; i++)
            {
                var param = new AxisParameter
                {
                    Name = $"Axis{i}",
                    AxisNum = i,
                    Step = 1000,
                    Speed = 100,
                    Acc = 0.1,
                    Dec = 0.1
                };
                _axes.Add(CreateAxis(param));
            }
        }

        #endregion

        #region 硬件状态查询

        /// <summary>
        /// 获取板卡总线错误码
        /// </summary>
        public ushort GetBusErrorCode()
        {
            lock (SyncRoot)
            {
                ushort errCode = 0;
                LTSMC.nmcs_get_errcode(_cardNo, 2, ref errCode);
                return errCode;
            }
        }

        /// <summary>
        /// 检查总线是否正常
        /// </summary>
        public bool IsBusHealthy()
        {
            return GetBusErrorCode() == 0;
        }

        /// <summary>
        /// 获取轴状态机状态
        /// </summary>
        public ushort GetAxisStateMachine(int axisIndex)
        {
            lock (SyncRoot)
            {
                ushort state = 0;
                LTSMC.nmcs_get_axis_state_machine(_cardNo, (ushort)axisIndex, ref state);
                return state;
            }
        }

        #endregion

        #region 全局操作

        /// <summary>
        /// 复位总线
        /// </summary>
        public bool ResetBus()
        {
            try
            {
                // 关闭并重新初始化
                Disconnect();
                System.Threading.Thread.Sleep(500);
                return Connect();
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
