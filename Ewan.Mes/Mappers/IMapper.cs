using System;

namespace Ewan.Mes.Mappers
{
    /// <summary>
    /// 双向映射器接口
    /// TDomain: 领域模型类型
    /// TDto: 数据传输对象类型
    /// </summary>
    public interface IMapper<TDomain, TDto>
    {
        /// <summary>
        /// 将领域模型转换为 DTO
        /// </summary>
        TDto ToDto(TDomain domain);

        /// <summary>
        /// 将 DTO 转换为领域模型
        /// </summary>
        TDomain ToDomain(TDto dto);
    }

    /// <summary>
    /// 单向映射器接口（仅 Domain -> Dto）
    /// 用于只需要发送不需要接收的场景
    /// </summary>
    public interface IToDtoMapper<TDomain, TDto>
    {
        /// <summary>
        /// 将领域模型转换为 DTO
        /// </summary>
        TDto ToDto(TDomain domain);
    }

    /// <summary>
    /// 单向映射器接口（仅 Dto -> Domain）
    /// 用于只需要接收不需要发送的场景
    /// </summary>
    public interface IToDomainMapper<TDomain, TDto>
    {
        /// <summary>
        /// 将 DTO 转换为领域模型
        /// </summary>
        TDomain ToDomain(TDto dto);
    }
}
