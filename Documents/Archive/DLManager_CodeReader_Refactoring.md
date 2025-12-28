# DLManager 与 Ewan.CodeReader 重构工作表

## 1. 背景与目标

### 1.1 当前状态分析

**DLManager.cs** (`Ewan.Core\ScanCode\DLManager.cs`)
- 位置：Ewan.Core 项目中
- 职责：扫码器管理器，作为应用层与底层扫码器库的桥梁
- 依赖：`Ewan.CodeReader` 程序集

**Ewan.CodeReader** (`Hardware\Ewan.CodeReader`)
- 统一扫码器封装库
- 当前支持：Datalogic（得利捷）、Hikvision（海康威视）
- 架构：工厂模式 + 接口抽象

### 1.2 现有问题

| 问题 | 描述 | 影响 |
|------|------|------|
| DLManager 命名不当 | "DL" 是 Datalogic 的缩写，但现在已支持多厂商 | 误导性命名 |
| DLManager 职责过重 | 包含配置加载、连接管理、扫码逻辑、结果规范化等 | 违反单一职责原则 |
| 扫码器特定逻辑泄漏 | `TriggerScanInternal()` 中直接判断 `DatalogicScanner` 类型 | 违反开闭原则 |
| 结果规范化重复 | `DLManager` 和 `DatalogicScanner` 中都有 `NormalizeScanResult` | 代码重复 |
| 扫码器设置分散 | `ApplyScannerSettings` 需要为每种扫码器类型单独处理 | 扩展性差 |
| 工厂扩展需修改源码 | 添加新扫码器类型需修改 `ScannerFactory` | 违反开闭原则 |

### 1.3 重构目标

1. **命名规范化**：将 DLManager 重命名为 `ScannerManager` 或 `CodeReaderManager`
2. **职责分离**：提取配置管理、结果处理等职责到独立组件
3. **可扩展架构**：支持运行时注册新扫码器类型，无需修改核心代码
4. **统一接口**：确保所有扫码器实现提供一致的触发扫码接口
5. **配置驱动**：通过配置文件支持扫码器参数设置

---

## 2. 重构方案

### 2.1 目标架构

```
Hardware/Ewan.CodeReader/
├── Interfaces/
│   ├── IScanner.cs                    # 扫码器接口（已有）
│   ├── IScannerConfiguration.cs       # 新增：扫码器配置接口
│   └── IScanResultNormalizer.cs       # 新增：结果规范化接口
├── Scanners/
│   ├── DatalogicScanner.cs            # 得利捷实现（优化）
│   └── HikvisionScanner.cs            # 海康实现（优化）
├── Configuration/
│   ├── ScannerConfiguration.cs        # 新增：通用配置类
│   ├── DatalogicConfiguration.cs      # 新增：得利捷配置
│   └── HikvisionConfiguration.cs      # 新增：海康配置
├── ScannerFactory.cs                  # 重构：支持注册式扩展
└── ScanResultNormalizer.cs            # 新增：统一结果规范化

Ewan.Core/ScanCode/
├── ScannerManager.cs                  # 重命名自 DLManager
└── DLManager.cs                       # 保留为兼容别名（标记 Obsolete）
```

### 2.2 IScanner 接口增强

```csharp
public interface IScanner : IDisposable
{
    // ... 现有成员

    /// <summary>
    /// 同步触发扫码并等待结果（新增统一接口）
    /// </summary>
    /// <param name="timeoutMs">超时时间（毫秒）</param>
    /// <returns>扫描结果</returns>
    string TriggerScanSync(int timeoutMs = 5000);

    /// <summary>
    /// 应用配置（新增）
    /// </summary>
    void ApplyConfiguration(IScannerConfiguration config);
}
```

### 2.3 配置接口设计

```csharp
public interface IScannerConfiguration
{
    /// <summary>
    /// 扫码器类型
    /// </summary>
    ScannerType ScannerType { get; }

    /// <summary>
    /// IP 地址
    /// </summary>
    string IpAddress { get; set; }

    /// <summary>
    /// 端口（TCP 类型使用）
    /// </summary>
    int Port { get; set; }

    /// <summary>
    /// 连接超时（毫秒）
    /// </summary>
    int ConnectionTimeoutMs { get; set; }

    /// <summary>
    /// 接收超时（毫秒）
    /// </summary>
    int ReceiveTimeoutMs { get; set; }
}

public class DatalogicConfiguration : IScannerConfiguration
{
    public ScannerType ScannerType => ScannerType.Datalogic;
    public string IpAddress { get; set; }
    public int Port { get; set; } = 51236;
    public int ConnectionTimeoutMs { get; set; } = 3000;
    public int ReceiveTimeoutMs { get; set; } = 5000;
    public string TriggerCommand { get; set; } = "T";
}

public class HikvisionConfiguration : IScannerConfiguration
{
    public ScannerType ScannerType => ScannerType.Hikvision;
    public string IpAddress { get; set; }
    public int Port { get; set; } // GigE 不使用端口，但保留以统一接口
    public int ConnectionTimeoutMs { get; set; } = 3000;
    public int ReceiveTimeoutMs { get; set; } = 1000;
}
```

### 2.4 工厂重构（注册式扩展）

```csharp
public static class ScannerFactory
{
    private static readonly Dictionary<ScannerType, Func<IScanner>> s_creators
        = new Dictionary<ScannerType, Func<IScanner>>
        {
            { ScannerType.Datalogic, () => new DatalogicScanner() },
            { ScannerType.Hikvision, () => new HikvisionScanner() },
        };

    /// <summary>
    /// 注册扫码器创建器
    /// </summary>
    public static void RegisterScanner(ScannerType type, Func<IScanner> creator)
    {
        s_creators[type] = creator ?? throw new ArgumentNullException(nameof(creator));
    }

    /// <summary>
    /// 创建扫描器实例
    /// </summary>
    public static IScanner CreateScanner(ScannerType type)
    {
        if (s_creators.TryGetValue(type, out var creator))
        {
            return creator();
        }
        throw new NotSupportedException($"不支持的扫描器类型: {type}");
    }

    /// <summary>
    /// 获取所有已注册的扫码器类型
    /// </summary>
    public static IEnumerable<ScannerType> GetRegisteredTypes() => s_creators.Keys;
}
```

### 2.5 ScannerManager 设计

```csharp
[Manager(Priority = 1)]
public class ScannerManager : IManager
{
    private static readonly Lazy<ScannerManager> s_instance = new Lazy<ScannerManager>(() => new ScannerManager());
    public static ScannerManager Instance() => s_instance.Value;

    private IScanner _scanner;
    private IScannerConfiguration _configuration;
    private readonly object _connectionLock = new object();

    public bool Init()
    {
        LoadConfiguration();
        return ConnectToScanner();
    }

    /// <summary>
    /// 加载配置（从 SystemParameters）
    /// </summary>
    private void LoadConfiguration()
    {
        var parameters = SystemParametersManager.Instance?.Parameters;
        var scannerType = ParseScannerType(parameters?.CodeReaderType);

        _configuration = CreateConfiguration(scannerType, parameters);
    }

    /// <summary>
    /// 根据类型创建对应配置
    /// </summary>
    private IScannerConfiguration CreateConfiguration(ScannerType type, SystemParameters parameters)
    {
        switch (type)
        {
            case ScannerType.Datalogic:
                return new DatalogicConfiguration
                {
                    IpAddress = parameters?.CodeReaderIp ?? "192.168.3.11",
                    Port = parameters?.CodeReaderPort ?? 51236,
                    TriggerCommand = parameters?.CodeReaderTriggerCommand ?? "T",
                    ConnectionTimeoutMs = parameters?.CodeReaderConnectionTimeoutMs ?? 3000,
                    ReceiveTimeoutMs = parameters?.CodeReaderReceiveTimeoutMs ?? 5000,
                };
            case ScannerType.Hikvision:
                return new HikvisionConfiguration
                {
                    IpAddress = parameters?.CodeReaderIp ?? "192.168.3.11",
                    ReceiveTimeoutMs = parameters?.CodeReaderReceiveTimeoutMs ?? 1000,
                };
            default:
                throw new NotSupportedException($"不支持的扫码器类型: {type}");
        }
    }

    /// <summary>
    /// 触发扫码（统一接口）
    /// </summary>
    public string TriggerScan()
    {
        lock (_connectionLock)
        {
            EnsureConnected();
            string result = _scanner.TriggerScanSync(_configuration.ReceiveTimeoutMs);
            return ScanResultNormalizer.Normalize(result);
        }
    }
}
```

---

## 3. 重构步骤

### 阶段 1：基础设施增强（Ewan.CodeReader）

| 步骤 | 任务 | 文件 | 状态 |
|------|------|------|------|
| 1.1 | 添加 `IScannerConfiguration` 接口 | Interfaces/IScannerConfiguration.cs | [x] |
| 1.2 | 添加 `DatalogicConfiguration` 类 | Configuration/DatalogicConfiguration.cs | [x] |
| 1.3 | 添加 `HikvisionConfiguration` 类 | Configuration/HikvisionConfiguration.cs | [x] |
| 1.4 | 添加 `ScanResultNormalizer` 类 | ScanResultNormalizer.cs | [x] |
| 1.5 | 在 `IScanner` 中添加 `TriggerScanSync` 方法 | Interfaces/IScanner.cs | [x] |
| 1.6 | 在 `IScanner` 中添加 `ApplyConfiguration` 方法 | Interfaces/IScanner.cs | [x] |

### 阶段 2：扫码器实现更新

| 步骤 | 任务 | 文件 | 状态 |
|------|------|------|------|
| 2.1 | `DatalogicScanner` 实现新接口方法 | Scanners/DatalogicScanner.cs | [x] |
| 2.2 | `HikvisionScanner` 实现新接口方法 | Scanners/HikvisionScanner.cs | [x] |
| 2.3 | 移除 `DatalogicScanner` 中的 `NormalizeScanResult` | Scanners/DatalogicScanner.cs | [x] |

### 阶段 3：工厂重构

| 步骤 | 任务 | 文件 | 状态 |
|------|------|------|------|
| 3.1 | 重构为注册式工厂 | ScannerFactory.cs | [x] |
| 3.2 | 添加 `GetRegisteredTypes` 方法 | ScannerFactory.cs | [x] |

### 阶段 4：管理器重构（Ewan.Core）

| 步骤 | 任务 | 文件 | 状态 |
|------|------|------|------|
| 4.1 | 创建 `ScannerManager` 类 | ScanCode/ScannerManager.cs | [x] |
| 4.2 | 删除 `DLManager` 类（不需要向后兼容） | ScanCode/DLManager.cs | [x] 已删除 |
| 4.3 | 更新所有调用方使用 `ScannerManager` | MaterialLoadingModule.cs, MaterialUnloadingModule.cs, MesManualSendViewModel.cs | [x] |

### 阶段 5：测试与验证

| 步骤 | 任务 | 状态 |
|------|------|------|
| 5.1 | 编译验证 | [x] |
| 5.2 | 单元测试（如有） | [-] 待添加 |
| 5.3 | 集成测试 | [-] 待添加 |

---

## 4. 兼容性考虑

### 4.1 向后兼容

- ~~`DLManager` 保留为 `ScannerManager` 的别名~~
- `DLManager` 已删除（用户确认不需要向后兼容）
- 所有调用方已更新为使用 `ScannerManager`

### 4.2 配置兼容

- `SystemParameters` 中的 `CodeReaderXxx` 属性保持不变
- 新增配置项采用可选参数模式

---

## 5. 扩展指南

### 5.1 添加新扫码器类型（如 Cognex）

1. **定义枚举值**
```csharp
// ScannerFactory.cs
public enum ScannerType
{
    Hikvision,
    Datalogic,
    Cognex,  // 新增
}
```

2. **实现 IScanner 接口**
```csharp
// Scanners/CognexScanner.cs
public class CognexScanner : IScanner
{
    // 实现所有接口方法
}
```

3. **创建配置类**
```csharp
// Configuration/CognexConfiguration.cs
public class CognexConfiguration : IScannerConfiguration
{
    public ScannerType ScannerType => ScannerType.Cognex;
    // 康耐视特定配置
}
```

4. **注册到工厂**
```csharp
// 应用启动时注册
ScannerFactory.RegisterScanner(ScannerType.Cognex, () => new CognexScanner());
```

5. **更新 ScannerManager 配置创建逻辑**
```csharp
case ScannerType.Cognex:
    return new CognexConfiguration { ... };
```

---

## 6. 风险评估

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|----------|
| 编译错误 | 中 | 中 | 逐步重构，每步验证编译 |
| 运行时兼容问题 | 低 | 高 | 保留 DLManager 别名 |
| 海康 SDK 缺失 | 低 | 低 | 条件编译已处理 |

---

## 7. 时间估算

| 阶段 | 预估工作量 |
|------|------------|
| 阶段 1 | 约 1-2 小时 |
| 阶段 2 | 约 1 小时 |
| 阶段 3 | 约 30 分钟 |
| 阶段 4 | 约 1-2 小时 |
| 阶段 5 | 约 30 分钟 |
| **总计** | **约 4-6 小时** |

---

## 8. 审核清单

- [ ] 所有新增代码遵循项目编码规范
- [ ] 日志输出使用中文
- [ ] 异常处理完善
- [ ] 资源正确释放（IDisposable）
- [ ] 线程安全考虑
- [ ] 编译通过（x64 Debug）
- [ ] 原有功能不受影响

---

*文档创建日期：2025-12-27*
*最后更新：2025-12-27*
