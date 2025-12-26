# UI 使用 MachineOperator（启动/停止/暂停/复位/清警）

核心对象：

- `IAlarmService`：报警中心（建议 DI 单例）
- `LogicRunner`：状态机线程队列（长期存活）
- `MachineOperator`：把 UI 常用按钮动作封装成约定（Start/Stop/Pause/Step/Home/ClearAlarm）

## WinForms（按钮事件）

```csharp
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using System;
using System.Windows.Forms;

public partial class MainForm : Form
{
    private readonly IAlarmService _alarms = new AlarmService();
    private readonly LogicRunner _runner = new LogicRunner();
    private readonly MachineOperator _op;

    private readonly IDisposable _stepSub;

    public MainForm()
    {
        InitializeComponent();

        _op = new MachineOperator(_alarms, _runner);

        // 1) 订阅报警刷新 UI（注意跨线程）
        _alarms.AlarmListChanged += (_, __) =>
        {
            if (IsDisposed) return;
            BeginInvoke((Action)RefreshAlarmUi);
        };

        // 2) 订阅步骤变化（LogicBase.SwitchIndex 每次变化都会广播 StepChanged）
        _stepSub = MessageHub.Current.SubscribeOnCurrentContext<StepChangedEventArgs>(args =>
        {
            if (IsDisposed) return;
            lblStep.Text = args.ToStep;
        });

        // 3) 捕获流程异常并报警 + 停机
        _runner.LogicException += (_, exArgs) =>
        {
            _alarms.AddAlarm(
                content: $"流程异常：{exArgs.LogicName}/{exArgs.Step} - {exArgs.Exception?.Message}",
                level: AlarmLevel.H,
                unit: "Logic",
                needReset: true,
                key: "Logic.Exception");

            _runner.Stop();
        };
    }

    private void btnStart_Click(object sender, EventArgs e)
    {
        // 如果报警未清除，Start 会返回 false（你可以提示用户先清警/复位）
        var ok = _op.Start(() => new MainLogic(/* 这里注入你的依赖 */));
        if (!ok)
        {
            MessageBox.Show("存在报警，无法启动。请先清除报警/复位。");
        }
    }

    private void btnPause_Click(object sender, EventArgs e) => _op.Pause();
    private void btnStop_Click(object sender, EventArgs e) => _op.Stop(clearQueue: false);
    private void btnStep_Click(object sender, EventArgs e) => _op.Step();

    private void btnHome_Click(object sender, EventArgs e)
    {
        // 复位/回原：通常只在 alarms.HasNeedResetAlarm == true 时启用按钮
        _op.Home(() => new HomeLogic(/* 这里注入你的依赖 */), clearAlarm: true);
    }

    private void btnClearAlarm_Click(object sender, EventArgs e) => _op.ClearAlarm();

    private void RefreshAlarmUi()
    {
        // 示例：刷新列表/按钮状态
        var list = _alarms.Snapshot;
        btnHome.Enabled = _alarms.HasNeedResetAlarm;
        lblAlarmCount.Text = _alarms.AlarmCount.ToString();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _stepSub.Dispose();
        _runner.Dispose();
        base.OnFormClosing(e);
    }
}
```

> `MainLogic/HomeLogic` 就是你写的 `LogicBase` 派生类（内部 `switch(SwitchIndex)`）。

## WPF（ViewModel + ICommand）

> 思路一样：按钮调用 `MachineOperator`，事件回调用 `Dispatcher` 切回 UI 线程更新绑定属性。

```csharp
using EwanCore.AlarmSystem;
using EwanCore.Messaging;
using EwanCore.StateMachine;
using System;

public sealed class MainVm : IDisposable
{
    private readonly IAlarmService _alarms;
    private readonly LogicRunner _runner;
    private readonly MachineOperator _op;
    private readonly IDisposable _stepSub;

    public string CurrentStep { get; private set; } = string.Empty;

    public MainVm(IAlarmService alarms)
    {
        _alarms = alarms;
        _runner = new LogicRunner();
        _op = new MachineOperator(_alarms, _runner);

        _alarms.AlarmListChanged += (_, __) =>
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 更新 Alarms 列表/按钮状态
            }));

        _stepSub = MessageHub.Current.SubscribeOnCurrentContext<StepChangedEventArgs>(args =>
        {
            CurrentStep = args.ToStep;
            // RaisePropertyChanged(nameof(CurrentStep));
        });
    }

    public void Start()
    {
        _op.Start(() => new MainLogic(/* 注入依赖 */));
    }

    public void Pause() => _op.Pause();
    public void Stop() => _op.Stop(clearQueue: false);
    public void Home() => _op.Home(() => new HomeLogic(/* 注入依赖 */), clearAlarm: true);
    public void ClearAlarm() => _op.ClearAlarm();

    public void Dispose()
    {
        _stepSub.Dispose();
        _runner.Dispose();
    }
}
```
