# StreamController 迁移/抽取建议（自动开启后台轮询）

你在 `EwanBusinessBonding/StreamController.cs` 里做的事情，本质上是：

- 组装一组 `IModule`（PLC 轮询、报警同步、心跳…）
- 各自塞进一个或多个 `StreamRunner`
- 在程序启动时自动 `Start()`，退出时 `Stop()`

## 为什么不建议把 StreamController 原样放进 EwanCommon

`StreamController` 目前强依赖项目专属模块/模型（例如 `PlcModule/AlarmModule<PlcModel>/PlcHeartModule`），抽到公共库会导致：

- 其它项目仍要改这个类（失去“通用库”价值）
- 公共库被迫引用业务工程/业务模型，依赖链变复杂
- 可插拔性差（不同项目开关模块、替换硬件模块更麻烦）

EwanCommon 更适合只放“框架件”：`StreamRunner/IModule`、生命周期宿主、消息/报警/状态机等。

## 推荐做法：每个项目写一个很薄的 BackgroundRunnerManager（IManager）

思路是：把 **“跑流程”** 的能力留在 `EwanCommon`（`StreamRunner`），把 **“组合哪些模块”** 留在各项目的 Composition Root（项目侧）。

你只需要在项目里新增一个类（名字随意），实现 `IManager`，并在 `Init()` 里组装 runner 并启动：

```csharp
using EwanCore;
using EwanCore.Attribute;
using EwanCore.Module.Interface;
using EwanCore.Runner;
using System.Collections.Generic;

[Manager(Priority = 2)]
public sealed class BackgroundRunnerManager : IManager
{
    private StreamRunner _mainRunner;
    private StreamRunner _heartRunner;

    public bool Init()
    {
        // 1) 主流程（示例：按你的项目决定有哪些模块）
        var mainModules = new List<IModule>
        {
            new PlcPollingModule(/* plc driver / snapshot store / msg bus */),
            new AlarmSyncModule(/* IAlarmService */),
            new MesSendModule(/* msg bus / mes client */),
        };
        _mainRunner = new StreamRunner(mainModules);
        _mainRunner.Start();

        // 2) 心跳流程（可选）
        var heartModules = new List<IModule>
        {
            new PlcHeartModule(/* ... */),
        };
        _heartRunner = new StreamRunner(heartModules);
        _heartRunner.Start();

        return true;
    }

    public void Dispose()
    {
        // 注意：Stop 内部会调用每个 module.Destroy()
        _heartRunner?.Stop();
        _mainRunner?.Stop();
    }
}
```

### 自动开启怎么实现？

- 如果你用 `ManagerLifetimeHost`：`host.Start()` 会调用所有 `[Manager]` 的 `Init()`，退出时 `host.Stop()`/`Dispose()` 会调用每个 manager 的 `Dispose()`，从而完成“自动开启/释放”。
- 不再支持 `BaseManager<T>` 静态单例风格：请统一使用 `ManagerLifetimeHost`（启动处 `host.Start()`，退出处 `host.Stop()` 或 `Dispose()`）。

### 模块开关/“标签禁用”怎么做？

项目侧按配置组装 `mainModules/heartModules`：

- 不需要某模块：不把它加入 list 即可
- 需要替换硬件：替换具体模块实现（`IModule`），不动框架代码

## StreamRunner 的使用注意点（避免卡死）

- `IModule.Run()` 建议“短、快、不阻塞”，频率控制由模块自己做（例如每 100ms 执行一次）
- 模块内部不要长时间等待网络/IO；需要等待的，建议异步化并用状态机/消息队列驱动
- 异常处理放在模块内部更好：避免某个模块抛异常导致整个 runner 行为不可控

## 对应示例

- `EwanCommon/examples/StreamRunnerExample.cs`：模块写法 + 消息推送示例
- `EwanCommon/examples/ManagerLifetimeHostExample.cs`：ManagerLifetimeHost 启动/释放示例
