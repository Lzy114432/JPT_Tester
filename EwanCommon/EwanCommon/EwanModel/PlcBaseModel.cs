using EwanCommon.Logging;
using EwanModel.Const;
using EwanModel.Plc;
using EwanModel.Plc.Interfaces;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EwanModel
{
    /// <summary>
    /// PLC 标签读写基类：通过反射读取 <see cref="PlcAttribute"/> 并完成数据解析/写入。
    /// 解析规则可通过组合策略替换：<see cref="IPlcValueCodec"/> / <see cref="IPlcAddressFormatter"/> / <see cref="IPlcBitIndexMapper"/>。
    /// </summary>
    public class PlcBaseModel : INotifyPropertyChanged
    {
        protected static readonly ILog s_logger = Log.GetLogger(typeof(PlcBaseModel));

        /// <summary>
        /// 设置的属性
        /// </summary>
        protected PropertyInfo[] mPropertyInfos;

        /// <summary>
        /// 读取的属性
        /// </summary>
        protected PropertyInfo[] mPropertyInfosOfRead;

        protected PropertyInfo[] mPropertyInfosOfReadBool;

        protected bool IsUpdateChangedProperty = false;

        protected List<string> ChangedPropertyNames = new List<string>();

        /// <summary>
        /// 地址格式化（可替换以适配特殊 PLC 地址规则）
        /// </summary>
        protected IPlcAddressFormatter AddressFormatter { get; set; } = new DefaultPlcAddressFormatter();

        /// <summary>
        /// bit 索引映射（可替换以适配 X/Y 等特殊地址映射）
        /// </summary>
        protected IPlcBitIndexMapper BitIndexMapper { get; set; } = new DefaultPlcBitIndexMapper();

        /// <summary>
        /// 值编解码（可替换以适配大小端/字节交换/字符串编码等）
        /// </summary>
        protected IPlcValueCodec ValueCodec { get; set; } = new DefaultPlcValueCodec();

        public event PropertyChangedEventHandler PropertyChanged;

        public delegate IOperateResult WriteDelegate(string address, object value);

        #region 读取解析数据
        /// <summary>
        /// 根据区块前缀查询对应的属性集合
        /// </summary>
        protected IList<PropertyInfo> GetPropertyInfoByPrefixAttr(PropertyInfo[] propertyInfos, string prefix)
        {
            var list = new List<PropertyInfo>();
            if (propertyInfos == null || propertyInfos.Length == 0 || string.IsNullOrEmpty(prefix))
            {
                return list;
            }

            foreach (var p in propertyInfos)
            {
                var attr = GetAttribute(p);
                if (attr == null)
                {
                    continue;
                }

                // 如果是X或者Y区块，需要特殊处理
                // 他们是公用一个区块的，所以需要特殊处理
                if (prefix.StartsWith(CommonConst.XSection) || prefix.StartsWith(CommonConst.YSection))
                {
                    if (attr.Prefix.StartsWith(CommonConst.XSection) || attr.Prefix.StartsWith(CommonConst.YSection))
                    {
                        list.Add(p);
                    }
                }
                else if (prefix.Equals(attr.Prefix))
                {
                    list.Add(p);
                }
            }
            return list;
        }

        protected PlcAttribute GetAttribute(PropertyInfo p)
        {
            return p.GetCustomAttribute<PlcAttribute>();
        }

        /// <summary>
        /// 解析 byte 数据块，并按 <paramref name="prefixName"/> 写回到对应属性。
        /// </summary>
        /// <param name="plcData">PLC 原始字节数据（按 word 顺序）。</param>
        /// <param name="prefixName">区块前缀（如 D/M/ZR...）。</param>
        public void ResolveByte(byte[] plcData, string prefixName)
        {
            if (plcData == null || plcData.Length == 0 || mPropertyInfosOfRead == null)
            {
                return;
            }

            var propertyInfos = GetPropertyInfoByPrefixAttr(mPropertyInfosOfRead, prefixName);
            foreach (var pInfo in propertyInfos)
            {
                var attr = GetAttribute(pInfo);
                if (attr == null)
                {
                    continue;
                }

                if (pInfo.PropertyType == typeof(bool))
                {
                    continue;
                }

                // 后续可以分块解析skips减去过的数据
                var offset = attr.Addr * 2; // 因为plcData是byte数组，所以这里要乘以2
                var len = GetBytesLength(pInfo.PropertyType, attr);
                if (len <= 0 || offset < 0 || offset + len > plcData.Length)
                {
                    continue;
                }

                if (ValueCodec != null && ValueCodec.TryDecode(pInfo.PropertyType, attr, plcData, offset, len, out var value))
                {
                    pInfo.SetValue(this, value);
                }
            }
        }

        /// <summary>
        /// 获取属性的字节长度
        /// </summary>
        private int GetBytesLength(Type type, PlcAttribute attr)
        {
            if (type == typeof(bool)) return 0; // bool 类型跳过处理
            if (type == typeof(string)) return attr?.Len ?? 0; // 特殊处理字符串类型
            return Marshal.SizeOf(type); // 对于其他基本类型
        }

        /// <summary>
        /// bool类型数据解析
        /// </summary>
        /// <param name="plcData">PLC bool 数据块。</param>
        /// <param name="prefixName">区块前缀（如 X/Y/M...）。</param>
        public void ResolveBool(bool[] plcData, string prefixName)
        {
            if (plcData == null || plcData.Length == 0 || mPropertyInfosOfRead == null)
            {
                return;
            }

            // 添加一个prefixName的解析功能，用于块处理数据解析
            var propertyInfos = GetPropertyInfoByPrefixAttr(mPropertyInfosOfRead, prefixName);
            foreach (var pInfo in propertyInfos)
            {
                var attr = GetAttribute(pInfo);
                if (attr == null)
                {
                    continue;
                }

                if (pInfo.PropertyType != typeof(bool))
                {
                    continue;
                }

                var index = BitIndexMapper?.MapIndex(attr.Prefix, attr.Addr) ?? attr.Addr;
                if (index < 0 || index >= plcData.Length)
                {
                    continue;
                }

                pInfo.SetValue(this, plcData[index]);
            }
        }

        protected PropertyInfo GetPropertyInfoByName(PropertyInfo[] propertyInfos, string pName)
        {
            return propertyInfos?.FirstOrDefault(o => o.Name.Equals(pName));
        }
        #endregion

        #region 写Plc
        /// <summary>
        /// 写PLC（保留原方法名以兼容旧使用方式）
        /// </summary>
        /// <param name="writeFunc">写入字节委托。</param>
        /// <param name="writeboolFunc">写入 bool 委托。</param>
        /// <returns>是否写入成功。</returns>
        public virtual bool SetKeyenceParam(
            Func<string, byte[], IOperateResult> writeFunc,
            Func<string, bool, IOperateResult> writeboolFunc)
        {
            if (writeFunc == null) throw new ArgumentNullException(nameof(writeFunc));
            if (writeboolFunc == null) throw new ArgumentNullException(nameof(writeboolFunc));

            try
            {
                if (mPropertyInfos == null || mPropertyInfos.Length == 0)
                {
                    return true;
                }

                foreach (var propertyInfo in mPropertyInfos)
                {
                    var attr = GetAttribute(propertyInfo);
                    if (attr == null)
                    {
                        continue;
                    }

                    // 选择是否只更新变化的属性
                    if (IsUpdateChangedProperty && (ChangedPropertyNames == null || !ChangedPropertyNames.Contains(propertyInfo.Name)))
                    {
                        continue;
                    }

                    if (propertyInfo.PropertyType == typeof(bool))
                    {
                        var address = AddressFormatter?.Format(attr.Addr, attr.Prefix, attr.BitIndex) ?? (attr.Prefix + attr.Addr);
                        var value = (bool)propertyInfo.GetValue(this);
                        var writeResult = writeboolFunc(address, value);
                        if (!writeResult.Success)
                        {
                            s_logger.Error($"SetKeyenceParam occur an exception {writeResult.ErrorMessage}");
                            return false;
                        }
                        continue;
                    }

                    var addressBytes = AddressFormatter?.Format(attr.Addr, attr.Prefix) ?? (attr.Prefix + attr.Addr);
                    var valueObj = propertyInfo.GetValue(this);

                    if (ValueCodec == null || !ValueCodec.TryEncode(propertyInfo.PropertyType, attr, valueObj, out var bytes))
                    {
                        throw new NotSupportedException($"Unsupported type: {propertyInfo.PropertyType}");
                    }

                    var writeBytesResult = writeFunc(addressBytes, bytes);
                    if (!writeBytesResult.Success)
                    {
                        s_logger.Error($"SetKeyenceParam occur an exception {writeBytesResult.ErrorMessage}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                s_logger.Error($"SetKeyenceParam occur an exception {ex}");
                return false;
            }
        }

        /// <summary>
        /// 启用“仅写变化属性”模式，并指定变化属性列表。
        /// </summary>
        /// <param name="changedPropertys">发生变化的属性名集合。</param>
        public virtual void UpdateChangedPropertyNames(List<string> changedPropertys)
        {
            IsUpdateChangedProperty = true;
            ChangedPropertyNames = changedPropertys ?? new List<string>();
        }
        #endregion
    }
}
