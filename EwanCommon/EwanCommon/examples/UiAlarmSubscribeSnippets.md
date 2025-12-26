# UI 订阅报警（WinForms / WPF）

`AlarmService`/`IAlarmService` 的事件可能来自后台线程（PLC 轮询、MES 模块、状态机线程），UI 订阅时需要切回 UI 线程刷新界面。

## 通用写法（推荐：WPF/WinForms 都能用）

在 UI 线程（Form 构造、Window Loaded、VM 初始化）里捕获 `SynchronizationContext`：

```csharp
using EwanCore.AlarmSystem;
using System;
using System.Threading;

public sealed class AlarmUiSubscriber : IDisposable
{
    private readonly IAlarmService _alarms;
    private readonly SynchronizationContext _ui;

    public AlarmUiSubscriber(IAlarmService alarms)
    {
        _alarms = alarms ?? throw new ArgumentNullException(nameof(alarms));
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        _alarms.AlarmListChanged += OnAlarmListChanged;
        // 或者订阅 _alarms.AlarmChanged（带 Added/Removed/Updated/Cleared 语义）
    }

    private void OnAlarmListChanged(object sender, EventArgs e)
    {
        _ui.Post(_ =>
        {
            // UI 线程里刷新列表/控件
            var snapshot = _alarms.Snapshot;
        }, null);
    }

    public void Dispose()
    {
        _alarms.AlarmListChanged -= OnAlarmListChanged;
    }
}
```

你在 `Post` 里做两件事就够了：

- `var list = alarms.Snapshot;` 拿到当前报警快照
- 刷新 DataGrid/ListView/报警灯/按钮状态（例如 `alarms.HasNeedResetAlarm`）

## WinForms 示例（DataGridView）

```csharp
using EwanCore.AlarmSystem;
using System;
using System.Linq;
using System.Windows.Forms;

public partial class MainForm : Form
{
    private readonly IAlarmService _alarms;

    public MainForm(IAlarmService alarms)
    {
        InitializeComponent();
        _alarms = alarms;

        _alarms.AlarmListChanged += (_, __) =>
        {
            if (IsDisposed) return;
            BeginInvoke((Action)RefreshAlarmGrid);
        };

        RefreshAlarmGrid();
    }

    private void RefreshAlarmGrid()
    {
        dataGridView1.DataSource = _alarms.Snapshot.ToList();
        btnHome.Enabled = _alarms.HasNeedResetAlarm;   // 需要复位的报警时启用“回原/复位”
    }
}
```

## WPF 示例（ObservableCollection）

```csharp
using EwanCore.AlarmSystem;
using System;
using System.Collections.ObjectModel;
using System.Windows;

public sealed class MainVm
{
    private readonly IAlarmService _alarms;
    public ObservableCollection<Alarm> Alarms { get; } = new ObservableCollection<Alarm>();

    public MainVm(IAlarmService alarms)
    {
        _alarms = alarms;
        _alarms.AlarmListChanged += (_, __) =>
            Application.Current.Dispatcher.BeginInvoke(new Action(Refresh));
        Refresh();
    }

    private void Refresh()
    {
        Alarms.Clear();
        foreach (var a in _alarms.Snapshot) Alarms.Add(a);
    }
}
```

