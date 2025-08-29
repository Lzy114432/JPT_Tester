# MarkingMachineFeeder - Claude Code Guide

This file provides comprehensive guidance for Claude Code (claude.ai/code) when working with this C# WPF application.

## Table of Contents

- [Quick Reference](#quick-reference)
- [Project Overview](#project-overview)
- [Architecture](#architecture)
- [Development Guidelines](#development-guidelines)
- [Security System](#security-system)
- [Internationalization](#internationalization)
- [Troubleshooting](#troubleshooting)
- [Build Information](#build-information)

## Quick Reference

### Essential Commands
```bash
# Build solution (verified working method)
cd "C:\Users\Administrator\source\repos\MarkingMachineFeeder" && "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" MarkingMachineFeeder.sln
```

### Log Files Location
```
# Application logs are located in:
MarkingMachineFeeder\bin\Debug\Logs\
- app.log[YYYY-MM-DD].txt (daily log files with dynamic dates)
- When user mentions "logs" or "日志", check this directory
```

### Key Singleton Pattern
```csharp
// ✅ Always use Instance() method (not property)
var securityManager = SecurityManager.Instance();
var cultureManager = CultureManager.Instance();
```

### Current Permission System (Simplified)
- **Resources**: `Language`, `PermissionConfig`
- **Actions**: `View`, `Control`
- **Admin**: All permissions | **Engineer**: View only | **Operator**: Language only

---

## Project Overview

**Technology Stack:** C# WPF, .NET Framework 4.7.2, Prism MVVM, log4net

**Solution Structure:**
```
MarkingMachineFeeder.sln
├── MarkingMachineFeeder     # Main WPF application
├── Ewan.Core                # Framework infrastructure
├── Ewan.Model               # Data models and constants
├── Ewan.BusinessBonding     # Business logic layer
└── Ewan.Resources           # Internationalization (EN/zh-CN)
```

**Key Features:**
- Role-based permission system with 3 user roles
- Dual-language support (English/Chinese)
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
        _uiLogger.Info(() => Ewan.Resources.LogMessages.MyManagerInitialized);
        return base.Init();
    }
}
```

**Key Components:**
- **SecurityManager**: Authentication & authorization
- **CultureManager**: Language switching & localization
- **UILogger**: Dual-output logging system
- **StreamRunner**: Module pipeline orchestration
- **MsgManager**: Thread-safe message queue

### MVVM Architecture

**ViewModel Base Pattern:**
```csharp
public class MyViewModel : BindableBase
{
    private readonly SecurityManager _securityManager = SecurityManager.Instance();
    private readonly CultureManager _cultureManager = CultureManager.Instance();
    private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
    
    // Subscribe to system events
    public MyViewModel()
    {
        _securityManager.UserAuthenticated += OnUserAuthenticated;
        _cultureManager.CultureChanged += OnCultureChanged;
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
// ✅ Always use resource-based logging for i18n
_uiLogger.Info(() => Ewan.Resources.LogMessages.OperationCompleted, parameter);
_uiLogger.Error(() => Ewan.Resources.LogMessages.DatabaseConnectionError, ex.Message);

// ❌ Never use hard-coded strings
_uiLogger.Info("Operation completed");
```

### 3. UI Control Development (MANDATORY PROCESS)

**Every UI control addition requires these 4 steps:**

**Step 1: Update Resource Files**
```xml
<!-- UIStrings.resx (English) -->
<data name="SaveButtonText"><value>Save</value></data>

<!-- UIStrings.zh-CN.resx (Chinese) -->
<data name="SaveButtonText"><value>保存</value></data>
```

**Step 2: ViewModel Property**
```csharp
private string _saveButtonText;
public string SaveButtonText
{
    get => _saveButtonText;
    set => SetProperty(ref _saveButtonText, value);
}
```

**Step 3: UpdateUITexts Method**
```csharp
private void UpdateUITexts()
{
    SaveButtonText = Ewan.Resources.UIStrings.SaveButtonText;
    RaisePropertyChanged(nameof(SaveButtonText)); // CRITICAL!
}
```

**Step 4: XAML Binding**
```xml
<Button Content="{Binding SaveButtonText}" Command="{Binding SaveCommand}" />
```

---

## Security System

### Current Permission Resources
- **Language**: Controls language switching functionality
- **PermissionConfig**: Controls access to permission configuration interface  
- **SystemControl**: Controls system operations (currently: application exit)

### Permission Matrix
| Role          | Language.Control | PermissionConfig.View | PermissionConfig.Control | SystemControl.Control |
| ------------- | ---------------- | --------------------- | ------------------------ | --------------------- |
| Administrator | ✓                | ✓                     | ✓                        | ✓                     |
| Engineer      | ✓                | ✓                     | ❌                        | ✓                     |
| Operator      | ✓                | ❌                     | ❌                        | ✓ (configurable)     |
| Guest         | ❌                | ❌                     | ❌                        | ❌                     |

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

### Culture Management
```csharp
public class MyViewModel : BindableBase
{
    private readonly CultureManager _cultureManager = CultureManager.Instance();
    
    public MyViewModel()
    {
        _cultureManager.CultureChanged += OnCultureChanged;
        
        // Sync initial culture
        Ewan.Resources.UIStrings.Culture = _cultureManager.CurrentCulture;
        UpdateUITexts();
    }
    
    private void OnCultureChanged(object sender, CultureChangedEventArgs e)
    {
        // Sync all resource cultures
        Ewan.Resources.UIStrings.Culture = e.NewCulture;
        Ewan.Resources.LogMessages.Culture = e.NewCulture;
        Ewan.Resources.PermissionConfigStrings.Culture = e.NewCulture;
        
        UpdateUITexts();
    }
}
```

### Resource Organization
- **UIStrings**: General UI text (buttons, labels, menus)
- **LogMessages**: System log messages
- **PermissionConfigStrings**: Permission configuration dialog text

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
- All `PermissionResources` references updated to use only `Language` and `PermissionConfig`
- Resource files contain all referenced strings
- `LogMessages.Designer.cs` has all required properties
- Using statements include `Ewan.Model.Security` for constants

#### Debug Logging
```csharp
// Add to permission methods for debugging
_uiLogger.Debug(() => Ewan.Resources.LogMessages.PermissionCheckResult, 
    _securityManager.CurrentUser?.Username ?? "未登录用户", 
    "Resource.Action", 
    hasPermission ? "有权限" : "无权限");
```

---

## Build Information

### Verified Build Command
```bash
cd "C:\Users\Administrator\source\repos\MarkingMachineFeeder" && "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" MarkingMachineFeeder.sln
```

### Known Non-blocking Warnings
- `LoginWindowViewModel._username` field assigned but never used (CS0414)

### Recently Verified Components
- ✅ PermissionConfigViewModel with permission-based command controls
- ✅ MainWindowViewModel with proper PropertyChanged notifications  
- ✅ SecurityManager permission checking system
- ✅ All resource file references resolved correctly
- ✅ UI permission updates working after authentication events

---

# Important Instructions
Do what has been asked; nothing more, nothing less.  
NEVER create files unless absolutely necessary for achieving your goal.  
ALWAYS prefer editing existing files to creating new ones.  
NEVER proactively create documentation files unless explicitly requested.