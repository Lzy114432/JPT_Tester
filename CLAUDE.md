# MarkingMachineFeeder - Claude Code Guide

This file provides comprehensive guidance for Claude Code (claude.ai/code) when working with this C# WPF application.

## Table of Contents

- [Quick Reference](#quick-reference)
- [Project Overview](#project-overview)
- [Architecture](#architecture)
- [Stream Process Management](#stream-process-management)
- [Development Guidelines](#development-guidelines)
- [Security System](#security-system)
- [Internationalization](#internationalization)
- [Troubleshooting](#troubleshooting)
- [Build Information](#build-information)

## Quick Reference

### Essential Commands
```bash
# Build solution with x64 platform (verified working method)
cd "C:\Users\Administrator\source\repos\MarkingMachineFeeder" && "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" MarkingMachineFeeder.sln -p:Configuration=Debug -p:Platform=x64
```

### Key Paths and Locations
```
# Project Root
C:\Users\Administrator\source\repos\MarkingMachineFeeder\

# Build Output (x64 Platform)
C:\Users\Administrator\source\repos\MarkingMachineFeeder\MarkingMachineFeeder\bin\x64\Debug\

# Application Logs
C:\Users\Administrator\source\repos\MarkingMachineFeeder\MarkingMachineFeeder\bin\x64\Debug\Logs\
- app.log[YYYY-MM-DD].txt (daily log files with dynamic dates)
- When user mentions "logs" or "日志", check this directory

# Configuration Files
C:\Users\Administrator\source\repos\MarkingMachineFeeder\MarkingMachineFeeder\bin\x64\Debug\users.json
C:\Users\Administrator\source\repos\MarkingMachineFeeder\MarkingMachineFeeder\bin\x64\Debug\config.json

# Module Locations
Ewan.Core\Module\         # Custom modules location
Ewan.Core\IO\            # IO control related classes
```

### Key Singleton Pattern
```csharp
// ✅ Always use Instance() method (not property)
var securityManager = SecurityManager.Instance();
```

### Current Permission System (Simplified)
- **Resources**: `PermissionConfig`, `SystemControl`, `HardwareControl` (language switching disabled)
- **Actions**: `View`, `Control`
- **Admin**: All permissions | **Engineer**: PermissionConfig.View + SystemControl + HardwareControl | **Operator**: SystemControl only

---

## Project Overview

**Technology Stack:** C# WPF, .NET Framework 4.7.2, Prism MVVM, log4net

**Solution Structure:**
```
MarkingMachineFeeder.sln
├── MarkingMachineFeeder     # Main WPF application
├── Ewan.Core                # Framework infrastructure
├── Ewan.Model               # Data models and constants
└── Ewan.BusinessBonding     # Business logic layer
```

**Key Features:**
- Role-based permission system with 3 user roles
- Chinese-only UI (i18n disabled)
- Unified logging (UI + file output)
- Modular architecture with manager pattern

---

## Architecture

### Core Framework (Ewan.Core)

**BaseManager\<T\> Pattern**
```csharp
[Manager(Priority = 0)]  // 0=Config, 1=External, 2=Modules, 99=Default
public class MyManager : BaseManager<MyManager>
{
    public override bool Init()
    {
        _uiLogger.Info("MyManager 初始化完成");
        return base.Init();
    }
}
```

**Key Components:**
- **SecurityManager**: Authentication & authorization
- **UILogger**: Dual-output logging system
- **StreamRunner**: Module pipeline orchestration
- **MsgManager**: Thread-safe message queue

### MVVM Architecture

**ViewModel Base Pattern:**
```csharp
public class MyViewModel : BindableBase
{
    private readonly SecurityManager _securityManager = SecurityManager.Instance();
    private readonly UILogger _uiLogger = new UILogger();
    
    // Subscribe to system events
    public MyViewModel()
    {
        _securityManager.UserAuthenticated += OnUserAuthenticated;
    }
}
```

---

## Stream Process Management

### Overview
The application uses a multi-stream architecture where different processes run independently and concurrently. Each stream is managed by a `StreamRunner` that executes a collection of `IModule` implementations.

### Stream Architecture

**Current Stream Types:**
1. **Main Stream** (`_mainRunner`) - Primary business logic
2. **PLC Heart Stream** (`_plcHeartRunner`) - PLC heartbeat monitoring
3. **Safety Stream** (`_safetyRunner`) - IO synchronization and safety monitoring

**Stream Components:**
```csharp
// StreamController.cs structure
public class StreamController : BaseManager<StreamController>
{
    // Stream Runners
    private StreamRunner _mainRunner;
    private StreamRunner _plcHeartRunner;
    private StreamRunner _safetyRunner;
    
    // Module Collections
    private List<IModule> _mainModules = new List<IModule>();
    private List<IModule> _plcHeartModules = new List<IModule>();
    private List<IModule> _safetyModules = new List<IModule>();
}
```

### Adding a New Stream Process

**Step 1: Create Module Class**
```csharp
// Location: Ewan.Core\Module\YourModule.cs
using Ewan.Core.Module;

public class YourModule : BaseModule<YourModule>
{
    protected override void OnInit()
    {
        // Initialize module resources
        _uiLogger.Info("模块初始化: {0}", nameof(YourModule));
    }
    
    protected override bool OnRun()
    {
        // Module logic - called in loop
        // Return true to continue, false to stop
        
        System.Threading.Thread.Sleep(100); // Control loop speed
        return true;
    }
    
    protected override void OnDestroy()
    {
        // Cleanup resources
        _uiLogger.Info("模块销毁: {0}", nameof(YourModule));
    }
}
```

**Step 2: Add Stream Runner and Module Collection**
```csharp
// In StreamController.cs

#region Stream Runners
private StreamRunner _yourStreamRunner;
#endregion

#region Module Collections
private List<IModule> _yourModules = new List<IModule>();
#endregion
```

**Step 3: Initialize Stream in Init()**
```csharp
public override bool Init()
{
    #region // Construct your stream nodes and add to runner
    
    // Add modules to collection
    _yourModules.Add(new YourModule());
    // Can add multiple modules to same stream
    // _yourModules.Add(new AnotherModule());
    
    // Create stream runner
    _yourStreamRunner = new StreamRunner(_yourModules);
    
    #endregion
    
    return base.Init();
}
```

**Step 4: Add Start/Stop Methods**
```csharp
// In StartRun()
public void StartRun()
{
    try
    {
        // ... existing streams
        
        // Start your stream
        StartYourStream();
    }
    catch (Exception ex)
    {
        // Handle errors
    }
}

// In StopRun()
public void StopRun()
{
    // ... existing streams
    
    // Stop your stream
    StopYourStream();
}

// Private methods
private void StartYourStream()
{
    if (_yourStreamRunner != null)
    {
        _yourStreamRunner.Start();
    }
}

private void StopYourStream()
{
    _yourStreamRunner?.Stop();
}
```

### Module Development Guidelines

**1. Module Lifecycle**
- `OnInit()`: Called once when module initializes
- `OnRun()`: Called repeatedly in a loop while stream is running
- `OnDestroy()`: Called once when module is destroyed

**2. Best Practices**
- Always include sleep/delay in `OnRun()` to control CPU usage
- Use `_uiLogger` for logging with resource strings
- Return `false` from `OnRun()` to stop the stream
- Handle exceptions properly to prevent stream crashes

**3. Common Module Patterns**

**Data Sync Module:**
```csharp
public class SafetyModule : BaseModule<SafetyModule>
{
    private LayeredIOManager _ioManager;
    private int _scanInterval = 10; // ms
    
    protected override bool OnRun()
    {
        if (_layeredIO != null && _ioManager.IsConnected)
        {
            _layeredIO.DataSync();
        }
        
        Thread.Sleep(_scanInterval);
        return true;
    }
}
```

**Monitoring Module:**
```csharp
public class PlcHeartModule : BaseModule<PlcHeartModule>
{
    private int _heartbeatInterval = 1000; // ms
    
    protected override bool OnRun()
    {
        // Check PLC status
        bool plcAlive = CheckPlcStatus();
        
        if (!plcAlive)
        {
            _uiLogger.Warn("PLC 心跳丢失");
        }
        
        Thread.Sleep(_heartbeatInterval);
        return true;
    }
}
```

### Stream Priority and Dependencies

**Initialization Order (by Manager Priority):**
1. Priority 0: SecurityManager (thread culture set in App.OnStartup)
2. Priority 1: LayeredIOManager
3. Priority 3: StreamController

**Stream Start Order (in StartRun):**
1. Main Stream
2. PLC Heart Stream  
3. Safety Stream
4. Other streams...

**Important:** Safety-critical streams should start first and stop last.

### Debugging Streams

**Check Stream Status:**
```csharp
// In StreamController
public bool IsMainStreamRunning => _mainRunner?.IsRunning ?? false;
public bool IsSafetyStreamRunning => _safetyRunner?.IsRunning ?? false;
```

**Log Stream Events:**
```csharp
private void StartSafetyStream()
{
    if (_safetyRunner != null)
    {
        _uiLogger.Info("即将启动 {0} 流", "Safety");
        _safetyRunner.Start();
        _uiLogger.Info("{0} 流已启动", "Safety");
    }
}
```

---

## Development Guidelines

### 1. Singleton Usage
```csharp
// ✅ Correct - Use Instance() method
var manager = SecurityManager.Instance();

// ❌ Wrong - Don't use Instance property
var manager = SecurityManager.Instance;
```

### 2. Logging Best Practices
```csharp
// ✅ i18n disabled: use hard-coded Chinese strings
_uiLogger.Info("操作完成: {0}", parameter);
_uiLogger.Error("数据库连接错误: {0}", ex.Message);

```

### 3. UI Control Development (MANDATORY PROCESS)

**Every UI control addition requires these 3 steps (Chinese-only UI):**

**Step 1: ViewModel Property (Chinese default)**
```csharp
private string _saveButtonText = "保存";
public string SaveButtonText
{
    get => _saveButtonText;
    set => SetProperty(ref _saveButtonText, value);
}
```

**Step 2: Update text and notify**
```csharp
SaveButtonText = "保存";
RaisePropertyChanged(nameof(SaveButtonText)); // CRITICAL!
```

**Step 3: XAML Binding**
```xml
<Button Content="{Binding SaveButtonText}" Command="{Binding SaveCommand}" />
```

---

## Security System

### Current Permission Resources
- **Language**: Reserved (language switching disabled)
- **PermissionConfig**: Controls access to permission configuration interface  
- **SystemControl**: Controls system operations (currently: application exit)
- **HardwareControl**: Controls hardware operations (IO control, etc.)

### Permission Matrix
| Role          | Language.Control | PermissionConfig.View | PermissionConfig.Control | SystemControl.Control | HardwareControl.Control |
| ------------- | ---------------- | --------------------- | ------------------------ | --------------------- | ----------------------- |
| Administrator | ✓                | ✓                     | ✓                        | ✓                     | ✓                       |
| Engineer      | ✓                | ✓                     | ❌                        | ✓                     | ✓                       |
| Operator      | ✓                | ❌                     | ❌                        | ✓ (configurable)     | ❌                       |
| Guest         | ❌                | ❌                     | ❌                        | ❌                     | ❌                       |

### Adding New Permission-Controlled Features

**Step 1: Define Permission Resource (if new category needed)**
```csharp
// In SecurityConstants.cs
public const string NewResource = "NewResource";
```

**Step 2: Add Permission to Roles (in SecurityManager.cs)**
```csharp
private Role CreateAdministratorRole()
{
    var role = new Role(RoleNames.Administrator, "系统管理员", "拥有所有权限");
    role.Permissions.AddRange(new[]
    {
        // ... existing permissions
        new Permission(PermissionResources.NewResource, PermissionActions.Control, "新功能控制")
    });
    return role;
}
```

**Step 3: Add UI Permission Configuration (in PermissionConfigViewModel.cs)**
```csharp
private List<PermissionConfig> CreateSystemPermissions()
{
    return new List<PermissionConfig>
    {
        // ... existing permissions
        new PermissionConfig { 
            PermissionId = "newresource.control", 
            DisplayName = "NewFeature", 
            Description = "NewFeatureDesc", 
            Category = "FeatureCategory" 
        },
    };
}
```

**Step 4: Add UI Visibility Control**
```csharp
// In ViewModel
public bool CanAccessNewFeature
{
    get => _canAccessNewFeature;
    set => SetProperty(ref _canAccessNewFeature, value);
}

private void UpdatePermissions()
{
    CanAccessNewFeature = _securityManager.HasPermission(PermissionResources.NewResource, PermissionActions.Control);
    RaisePropertyChanged(nameof(CanAccessNewFeature));
}
```

**Step 5: XAML Binding with Visibility Control**
```xml
<MenuItem Header="New Feature" 
          Command="{Binding NewFeatureCommand}"
          Visibility="{Binding CanAccessNewFeature, Converter={StaticResource BooleanToVisibilityConverter}}"/>
```

**Step 6: Add Localized Resources**
```xml
<!-- UIStrings.resx -->
<data name="NewFeature"><value>New Feature</value></data>

<!-- UIStrings.zh-CN.resx -->  
<data name="NewFeature"><value>新功能</value></data>

<!-- PermissionConfigStrings.resx -->
<data name="NewFeature"><value>New Feature Access</value></data>
<data name="NewFeatureDesc"><value>Allow access to new feature</value></data>
```

### User Permission Configuration Process

**Method 1: Through PermissionConfigWindow (Recommended)**
1. Login as Administrator
2. Open System → Permission Configuration
3. Select the role to modify
4. Check/uncheck desired permissions
5. Click Save to persist changes

**Method 2: Direct users.json Modification**
⚠️ **IMPORTANT**: Permission keys in users.json must use **lowercase format**
```json
{
  "Resource": "systemcontrol",  // NOT "SystemControl"
  "Action": "Control",          // Action can be mixed case
  "Key": "systemcontrol.Control"
}
```

**Common Permission Keys:**
- `language.Control` - Language switching
- `permissionconfig.View` - View permission config  
- `permissionconfig.Control` - Modify permission config
- `systemcontrol.Control` - Exit application

**Example: Remove Exit Permission from Operator**
```json
{
  "Name": "Operator",
  "Permissions": [
    {
      "Resource": "language",
      "Action": "Control",
      "Key": "language.Control"
    }
    // Remove systemcontrol.Control entry to disable exit permission
  ]
}
```

### Permission-Based Commands
```csharp
public class PermissionConfigViewModel : BindableBase
{
    private readonly SecurityManager _securityManager = SecurityManager.Instance();
    
    public DelegateCommand SaveCommand { get; }
    public DelegateCommand ApplyCommand { get; }
    
    public PermissionConfigViewModel()
    {
        // Listen for authentication changes
        _securityManager.UserAuthenticated += OnUserAuthenticated;
        _securityManager.UserLoggedOut += OnUserLoggedOut;
        
        // Commands with permission checks
        SaveCommand = new DelegateCommand(ExecuteSave, CanExecutePermissionControl);
        ApplyCommand = new DelegateCommand(ExecuteApply, CanExecutePermissionControl);
    }
    
    private bool CanExecutePermissionControl()
    {
        return _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.Control);
    }
    
    private void OnUserAuthenticated(object sender, User user)
    {
        RefreshCommandStates(); // Update UI after login
    }
    
    private void RefreshCommandStates()
    {
        SaveCommand.RaiseCanExecuteChanged();
        ApplyCommand.RaiseCanExecuteChanged();
    }
}
```

### Authentication Flow
```csharp
// Login
if (_securityManager.Authenticate(username, password))
{
    // Success - UI will automatically update via events
}

// Permission Check
if (_securityManager.HasPermission(PermissionResources.Language, PermissionActions.Control))
{
    // User can switch language
}

// Role Check
if (_securityManager.HasRole(RoleNames.Administrator))
{
    // User is admin
}
```

---

## Internationalization

### Status
Internationalization is disabled. UI/log strings are hard-coded in Chinese, and `App.OnStartup` fixes the thread culture to `zh-CN`.

---

## Troubleshooting

### Common Issues & Solutions

#### Problem: Admin user shows no permissions after login

**Symptoms:**
- Login succeeds but permission-based buttons remain disabled
- UI doesn't reflect user permissions

**Solutions:**
1. **Missing PropertyChanged notifications**
```csharp
private void UpdatePermissions()
{
    CanViewSettings = _securityManager.HasPermission(PermissionResources.PermissionConfig, PermissionActions.View);
    
    // CRITICAL: Must trigger PropertyChanged
    RaisePropertyChanged(nameof(CanViewSettings));
    
    // CRITICAL: Refresh command states
    OpenPermissionConfigCommand.RaiseCanExecuteChanged();
}
```

2. **Not listening to authentication events**
```csharp
public MyViewModel()
{
    _securityManager.UserAuthenticated += OnUserAuthenticated;
    _securityManager.UserLoggedOut += OnUserLoggedOut;
}

private void OnUserAuthenticated(object sender, User user)
{
    UpdatePermissions(); // Refresh UI permissions
}
```

#### Problem: Permission configuration changes not taking effect

**Symptoms:**
- Changed permissions in PermissionConfigWindow but UI still shows old permissions
- User permissions don't match users.json file

**Solutions:**
1. **Check permission key format in users.json**
```json
// ✅ CORRECT - Resource in lowercase
{
  "Resource": "systemcontrol",
  "Action": "Control", 
  "Key": "systemcontrol.Control"
}

// ❌ WRONG - Resource capitalization mismatch
{
  "Resource": "SystemControl",  // This won't match!
  "Action": "Control",
  "Key": "SystemControl.Control"
}
```

2. **Restart application after manual users.json changes**
   - users.json is loaded on application startup
   - Manual edits require restart to take effect
   - Use PermissionConfigWindow for live updates

3. **Verify permission constants match PermissionId format**
```csharp
// SecurityConstants.cs
public const string SystemControl = "SystemControl"; 

// PermissionConfigViewModel.cs - must use lowercase in PermissionId
new PermissionConfig { 
    PermissionId = "systemcontrol.control", // lowercase to match users.json
    DisplayName = "SystemControl",
    Category = "SystemControl" 
}
```

#### Problem: New UI element not respecting permissions

**Symptoms:**
- Added new button/menu but it's always visible regardless of user permissions
- Permission checking works but UI doesn't update

**Solutions:**
1. **Missing Visibility binding in XAML**
```xml
<!-- ✅ CORRECT -->
<MenuItem Header="Exit" 
          Command="{Binding ExitCommand}"
          Visibility="{Binding CanExit, Converter={StaticResource BooleanToVisibilityConverter}}"/>

<!-- ❌ WRONG - Missing Visibility binding -->
<MenuItem Header="Exit" Command="{Binding ExitCommand}"/>
```

2. **Missing permission property in ViewModel**
```csharp
// Add property for permission state
public bool CanExit
{
    get => _canExit;
    set => SetProperty(ref _canExit, value);
}

// Update in UpdatePermissions method
private void UpdatePermissions()
{
    CanExit = _securityManager.HasPermission(PermissionResources.SystemControl, PermissionActions.Control);
    RaisePropertyChanged(nameof(CanExit)); // CRITICAL!
}
```

3. **Add debugging output**
```csharp
private void UpdatePermissions()
{
    CanExit = _securityManager.HasPermission(PermissionResources.SystemControl, PermissionActions.Control);
    
    // Debug output
    var user = _securityManager.CurrentUser?.Username ?? "未登录";
    System.Diagnostics.Debug.WriteLine($"用户 {user} 退出权限: {(CanExit ? "有权限" : "无权限")}");
    
    RaisePropertyChanged(nameof(CanExit));
}
```

#### Problem: Compilation errors after permission changes

**Check these items:**
- All `PermissionResources` references match configured permissions (PermissionConfig/SystemControl/HardwareControl)
- Using statements include `Ewan.Model.Security` for constants

#### Debug Logging
```csharp
// Add to permission methods for debugging
_uiLogger.Debug("权限检查结果: 用户={0}, 权限={1}, 结果={2}",
    _securityManager.CurrentUser?.Username ?? "未登录用户",
    "Resource.Action",
    hasPermission ? "有权限" : "无权限");
```

---

## Build Information

### Verified Build Command
```bash
# Build with x64 platform configuration (required for proper compilation)
cd "C:\Users\Administrator\source\repos\MarkingMachineFeeder" && "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" MarkingMachineFeeder.sln -p:Configuration=Debug -p:Platform=x64

# Note: Use hyphen (-p:) instead of forward slash (/p:) for MSBuild parameters to avoid parsing errors
```

### Known Non-blocking Warnings
- `LoginWindowViewModel._username` field assigned but never used (CS0414)

### Recently Verified Components
- ✅ PermissionConfigViewModel with permission-based command controls
- ✅ MainWindowViewModel with proper PropertyChanged notifications  
- ✅ SecurityManager permission checking system
- ✅ All UI/log strings hard-coded in Chinese
- ✅ UI permission updates working after authentication events

---

# Important Instructions
Do what has been asked; nothing more, nothing less.  
NEVER create files unless absolutely necessary for achieving your goal.  
ALWAYS prefer editing existing files to creating new ones.  
NEVER proactively create documentation files unless explicitly requested.
