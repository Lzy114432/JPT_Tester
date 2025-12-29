# MES 环型线信号集成任务清单

> **项目**: MarkingMachineFeeder
> **创建日期**: 2025-12-29
> **目标**: 将环型线 MES 信号交互集成到现有 Logic 状态机中

---

## 一、架构概览

### 现有架构

```
┌──────────────────────────────────────────────────────────────────┐
│                         应用层 (Application)                      │
├──────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────────┐  ┌─────────────────────┐   │
│  │ MainLogic   │  │MaterialLoading  │  │MaterialUnloading    │   │
│  │             │  │Logic            │  │Logic                │   │
│  └─────────────┘  └─────────────────┘  └─────────────────────┘   │
│                              ↓                                    │
├──────────────────────────────────────────────────────────────────┤
│                      消息总线 (MessageHub)                        │
│  ┌────────────────┐  ┌─────────────────┐  ┌──────────────────┐   │
│  │MesRingLineReq  │  │MesRingLineFeed  │  │BinElevatorMsg    │   │
│  │               ↓│  │back             │  │                  │   │
│  └────────────────┘  └─────────────────┘  └──────────────────┘   │
├──────────────────────────────────────────────────────────────────┤
│                      MES 模块层 (MesModule)                       │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ MesModule (消息驱动) → 处理 MesRingLineRequest             │  │
│  │    - RespondAsync<MesRingLineRequest, MesRingLineFeedback> │  │
│  └────────────────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────────────┤
│                      MES 管理层 (MesManager)                      │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ MesManager.Instance()                                      │  │
│  │    - Configure/Connect/Disconnect                          │  │
│  │    - InitializeRingLine(deviceId, deviceCode)              │  │
│  │    - PublishXxxAsync() 系列方法                            │  │
│  │    - OnFeedingQianLiaocangResponse()                       │  │
│  └────────────────────────────────────────────────────────────┘  │
├──────────────────────────────────────────────────────────────────┤
│                      服务层 (Ewan.Mes)                            │
│  ┌──────────────┐  ┌───────────────┐  ┌────────────────────┐    │
│  │RingLineService│ │RingLineTopics │  │Domain Models       │    │
│  │(IRingLineService)│             │  │FeedingXxxData等    │    │
│  └──────────────┘  └───────────────┘  └────────────────────┘    │
├──────────────────────────────────────────────────────────────────┤
│                      传输层 (Transport)                          │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │ MqttMessageTransport → MqttClientWrapper                   │  │
│  │    - MQTT Broker: 172.24.10.28:1883                        │  │
│  │    - QoS: 2                                                │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### 数据流

```
Logic 状态机
    ↓ (发送 MesRingLineRequest)
MessageHub.Current.RequestAsync<>
    ↓
MesModule.ProcessRequestAsync()
    ↓
MesManager.PublishXxxAsync()
    ↓
RingLineService (Ewan.Mes)
    ↓
MqttMessageTransport
    ↓
MQTT Broker (智慧工厂平台)
```

---

## 二、信号交互对照表

| 编号 | MQTT 主题 | MesRingLineAction | 触发位置 | 备注 |
|------|-----------|-------------------|----------|------|
| 1 | `/device/{ID}/up/feeding_qian_liaocang` | FeedingQianLiaocang | MaterialLoadingLogic | 前料仓上料请求，等待响应 |
| - | `/device/{ID}/down/feeding_qian_liaocang_response/{code}` | (响应) | MesModule订阅 | 平台返回 A/B 单工单号 |
| - | `/device/{ID}/up/feeding_qian_liaocang_success` | FeedingQianLiaocangSuccess | MaterialLoadingLogic | 上料成功确认 |
| 2 | `/device/{ID}/up/unloading_qian_liaocang` | UnloadingQianLiaocang | MaterialUnloadingLogic | 前料仓下料 |
| 3 | `/device/{ID}/up/feeding_zhong_liaocang` | FeedingZhongLiaocang | - | 中料仓上料 |
| 4 | `/device/{ID}/up/unloading_zhong_liaocang` | UnloadingZhongLiaocang | - | 中料仓下料 |
| 5 | `/device/{ID}/up/feeding_qingxihongganji` | FeedingQingxihongganji | - | 清洗烘干机上料 |
| 6 | `/device/{ID}/up/feeding_hou_liaocang` | FeedingHouLiaocang | - | 后料仓上料 |
| 7 | `/device/{ID}/up/unloading_hou_liaocang` | UnloadingHouLiaocang | - | 后料仓下料 |

---

## 三、任务清单

### 阶段 1: MaterialLoadingLogic 集成 MES 上报

**目标**: 在装料逻辑中，扫码后向 MES 发送上料请求并等待响应

#### Task 1.1: 修改 MaterialLoadingLogic 添加 MES 发送状态

**文件**: `Ewan.Core\Logic\MaterialLoadingLogic.cs`

**修改内容**:
1. 添加私有字段：
```csharp
private Task<MesRingLineFeedback> _mesFeedingTask;
private string _billNoA = string.Empty;
private string _billNoB = string.Empty;
```

2. 在 `执行扫码` 状态后添加新状态 `发送MES上料请求`：
```csharp
case "发送MES上料请求":
    if (_mesFeedingTask == null)
    {
        var request = new MesRingLineRequest
        {
            Action = MesRingLineAction.FeedingQianLiaocang,
            PlateCode = _scannedCode,
            BillNoWip = string.Empty, // 或从配置获取在制工单号
            TimeoutMs = 30000
        };
        _mesFeedingTask = MessageHub.Current.RequestAsync<MesRingLineRequest, MesRingLineFeedback>(
            request, timeoutMs: 35000);
    }

    if (!_mesFeedingTask.IsCompleted)
        return;

    try
    {
        var feedback = _mesFeedingTask.Result;
        if (feedback.Success)
        {
            var responseData = feedback.Data as FeedingQianLiaocangResponseData;
            _billNoA = responseData?.BillNoA ?? string.Empty;
            _billNoB = responseData?.BillNoB ?? string.Empty;
            _uiLogger.InfoRaw("MES上料响应: A单={0}, B单={1}", _billNoA, _billNoB);
            SwitchIndex = "移动到料仓";
        }
        else
        {
            _uiLogger.WarnRaw("MES上料请求失败: {0}", feedback.Message);
            // 根据业务决定：继续还是报警
            SwitchIndex = "移动到料仓"; // 或触发报警
        }
    }
    catch (Exception ex)
    {
        _uiLogger.ErrorRaw("MES上料请求异常: {0}", ex.Message);
        SwitchIndex = "移动到料仓";
    }
    finally
    {
        _mesFeedingTask = null;
    }
    break;
```

3. 修改 `执行扫码` 状态的切换目标：
```csharp
// 原: SwitchIndex = "移动到料仓";
// 改为:
if (_parametersManager?.Parameters?.MesEnabled == true)
{
    SwitchIndex = "发送MES上料请求";
}
else
{
    SwitchIndex = "移动到料仓";
}
```

4. 在 `清理状态` 中添加 MES 上料成功确认：
```csharp
if (_parametersManager?.Parameters?.MesEnabled == true && !string.IsNullOrWhiteSpace(_scannedCode))
{
    var successRequest = new MesRingLineRequest
    {
        Action = MesRingLineAction.FeedingQianLiaocangSuccess,
        PlateCode = _scannedCode,
        FeedingLiaokuangCode = GetLiaokuangCode(_targetBin)
    };
    MessageHub.Current.Post(successRequest); // Fire-and-forget
}
```

5. 在 `Rset()` 中重置新增字段：
```csharp
_mesFeedingTask = null;
_billNoA = string.Empty;
_billNoB = string.Empty;
```

---

#### Task 1.2: 添加辅助方法

**文件**: `Ewan.Core\Logic\MaterialLoadingLogic.cs`

```csharp
/// <summary>
/// 获取料框编号
/// </summary>
private string GetLiaokuangCode(int binNumber)
{
    // 根据实际业务逻辑返回料框编号
    // 可能从系统参数或运行时状态获取
    return $"BIN{binNumber:D2}";
}
```

---

### 阶段 2: MaterialUnloadingLogic 集成 MES 上报

**目标**: 在卸料逻辑中，向 MES 发送下料信号

#### Task 2.1: 修改 MaterialUnloadingLogic 添加 MES 发送

**文件**: `Ewan.Core\Logic\MaterialUnloadingLogic.cs`

**修改内容**:
1. 在 `发送Modbus完成` 状态前添加 `发送MES下料信号` 状态：
```csharp
case "发送MES下料信号":
    if (_parametersManager?.Parameters?.MesEnabled == true && !string.IsNullOrWhiteSpace(_lastScannedQrCode))
    {
        var request = new MesRingLineRequest
        {
            Action = MesRingLineAction.UnloadingQianLiaocang,
            PlateCode = _lastScannedQrCode,
            FeedingLiaokuangCode = GetLiaokuangCode(_selectedBin)
        };

        // Fire-and-forget，不等待响应
        MessageHub.Current.Post(request);
        _uiLogger.InfoRaw("已发送MES下料信号: {0}", _lastScannedQrCode);
    }

    SwitchIndex = "发送Modbus完成";
    break;
```

2. 修改 `等待放入完成` 状态的切换目标：
```csharp
// 原: SwitchIndex = "发送Modbus完成";
// 改为:
SwitchIndex = "发送MES下料信号";
```

3. 添加辅助方法：
```csharp
private string GetLiaokuangCode(int binNumber)
{
    return $"BIN{binNumber:D2}";
}
```

---

### 阶段 3: MesModule 优化（可选）

#### Task 3.1: 支持 Fire-and-Forget 模式

**文件**: `Ewan.Core\Module\MesModule.cs`

**修改内容**: 添加对 Post 消息的处理（非 Request-Response 模式）

```csharp
private IDisposable _postSubscription;

protected override void OnInit()
{
    // 现有代码...

    // 添加 Post 消息订阅
    _postSubscription = MessageHub.Current.Subscribe<MesRingLineRequest>(
        ProcessPostRequest);
}

private void ProcessPostRequest(MesRingLineRequest request)
{
    if (request == null) return;

    try
    {
        string error;
        if (!EnsureMesReady(out error))
        {
            _uiLogger.WarnRaw("MES Post请求跳过: {0}", error);
            return;
        }

        // 直接发送，不等待响应
        switch (request.Action)
        {
            case MesRingLineAction.FeedingQianLiaocangSuccess:
                MesManager.Instance().PublishFeedingQianLiaocangSuccess(
                    new FeedingQianLiaocangSuccessData
                    {
                        DeviceCode = MesManager.Instance().RingLineDeviceCode,
                        PlateCode = request.PlateCode,
                        FeedingLiaokuangCode = request.FeedingLiaokuangCode,
                        Timestamp = DateTime.Now
                    });
                break;
            // 其他 Action 类似处理...
        }
    }
    catch (Exception ex)
    {
        _uiLogger.ErrorRaw("MES Post请求异常: {0}", ex.Message);
    }
}

protected override void OnDestroy()
{
    // 现有代码...
    _postSubscription?.Dispose();
    _postSubscription = null;
}
```

---

### 阶段 4: 配置与测试

#### Task 4.1: 确认系统参数配置

**文件**: `Ewan.Model\System\SystemParameters.cs` (已存在)

确认包含以下参数：
- `MesEnabled` - MES 启用开关
- `MesBrokerHost` - MQTT Broker 地址
- `MesBrokerPort` - MQTT 端口
- `MesUserName` - 用户名
- `MesPassword` - 密码
- `MesRingLineDeviceId` - 设备 ID
- `MesRingLineDeviceCode` - 设备编码

#### Task 4.2: 测试场景

| 场景 | 预期行为 |
|------|----------|
| MES 禁用时装料 | 跳过 MES 发送，正常完成装料 |
| MES 启用时装料 | 扫码后发送请求，等待响应，成功后发送确认 |
| MES 请求超时 | 记录警告日志，继续后续流程 |
| MES 启用时卸料 | 放入小车后发送下料信号 |

---

## 四、代码变更汇总

| 文件 | 变更类型 | 说明 |
|------|----------|------|
| `Ewan.Core\Logic\MaterialLoadingLogic.cs` | 修改 | 添加 MES 上料请求和成功确认 |
| `Ewan.Core\Logic\MaterialUnloadingLogic.cs` | 修改 | 添加 MES 下料信号发送 |
| `Ewan.Core\Module\MesModule.cs` | 修改(可选) | 支持 Fire-and-Forget 模式 |

---

## 五、依赖关系

```
MaterialLoadingLogic
    ├── MessageHub (EwanCore.Messaging)
    ├── MesRingLineRequest/Feedback (Ewan.Model.Messages)
    └── FeedingQianLiaocangResponseData (Ewan.Mes.Models.Domain.ZHJW.RingLine)

MaterialUnloadingLogic
    ├── MessageHub (EwanCore.Messaging)
    └── MesRingLineRequest (Ewan.Model.Messages)

MesModule (已实现)
    ├── MesManager
    └── IRingLineService
```

---

## 六、注意事项

1. **线程安全**: `_mesFeedingTask` 的访问需确保线程安全
2. **超时处理**: MES 请求超时不应阻塞主流程
3. **重试机制**: 当前设计不包含重试，可根据需求添加
4. **日志级别**: MES 通信失败使用 Warn 级别，异常使用 Error 级别
5. **配置热更新**: MES 参数变更需重新初始化 RingLineService

---

## 七、后续扩展

- [ ] 添加中料仓/后料仓的 MES 上报（需要物理支持）
- [ ] 添加清洗烘干机的 MES 上报
- [ ] 实现 MES 断线重连时的消息缓存和重发
- [ ] 添加 MES 通信状态的 UI 显示
