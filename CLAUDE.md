# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a C# WPF application built with .NET Framework 4.7.2 called "MarkingMachineFeeder". The solution consists of multiple projects:

- **MarkingMachineFeeder** - Main WPF application (entry point)
- **Ewan.Core** - Core framework library providing base infrastructure
- **Ewan.Model** - Data models and domain objects  
- **Ewan.BusinessBonding** - Business logic layer
- **Ewan.Resources** - Internationalization resources and localized strings

## Project Structure and Key Files

### MarkingMachineFeeder (Main Application)
```
MarkingMachineFeeder/
├── App.xaml/App.xaml.cs          # Application entry point and global configuration
├── MainWindow.xaml/.cs           # Main application window
├── log4net.config                # log4net logging configuration
├── Viewmodel/
│   ├── MainWindowViewModel.cs    # Main window MVVM view model
│   └── LogWindowViewModel.cs     # Log display window view model
└── Window/
    ├── LogWindow.xaml/.cs        # Log display user control with auto-scroll
    └── [Future windows]          # Additional UI windows
```

### Ewan.Core (Framework Library)
```
Ewan.Core/
├── BaseManager.cs                # Generic singleton base class with UILogger
├── Attribute/
│   └── ManagerAttribute.cs       # Attribute for automatic manager initialization
├── Culture/
│   └── CultureManager.cs         # Internationalization and language switching
├── Logger/
│   └── UILogger.cs               # Unified logging system (UI + file output)
├── Module/
│   ├── BaseModule.cs             # Abstract base for pipeline modules
│   └── Interface/IModule.cs      # Module interface contract
├── Msg/
│   ├── MsgManager.cs             # Thread-safe message queue system
│   ├── MsgListener.cs            # Message listener for observer pattern
│   ├── MsgSubject.cs             # Enumeration of message subjects
│   └── MessageModel.cs           # Message wrapper/container
├── Runner.cs                     # StreamRunner for module pipeline orchestration
└── Utils/
    └── DateTimeUtil.cs           # Date/time utility functions
```

### Ewan.Model (Data Models)
```
Ewan.Model/
├── Messages/
│   └── UILogMsg.cs              # Data transfer object for log messages
├── Msg/                         # Message-related data models
└── [Future models]              # Domain objects and DTOs
```

### Ewan.BusinessBonding (Business Logic)
```
Ewan.BusinessBonding/
└── MainController.cs            # Main application controller with manager initialization
```

### Ewan.Resources (Internationalization)
```
Ewan.Resources/
├── LogMessages.resx             # English log messages (default)
├── LogMessages.zh-CN.resx       # Chinese log messages
└── LogMessages.Designer.cs      # Auto-generated strongly-typed resource accessor
```

## Key Configuration Files

### log4net.config
Located in the main application, this file configures:
- File appenders for persistent logging
- Console appenders for debug output
- Log levels and formatting patterns
- Rolling file policies

### App.config
Application configuration including:
- Framework target version
- Assembly binding redirects
- Application-specific settings

## Build Commands

```bash
# Build entire solution
msbuild MarkingMachineFeeder.sln

# Build in Debug configuration
msbuild MarkingMachineFeeder.sln /p:Configuration=Debug

# Build in Release configuration  
msbuild MarkingMachineFeeder.sln /p:Configuration=Release

# Clean solution
msbuild MarkingMachineFeeder.sln /t:Clean

# Rebuild solution
msbuild MarkingMachineFeeder.sln /t:Rebuild
```

## Architecture Overview

### Ewan.Core Framework
The core framework implements a modular, message-driven architecture:

- **BaseManager<T>** - Generic singleton base class for managers with logging (Ewan.Core\BaseManager.cs:9)
- **StreamRunner** - Orchestrates execution of module pipelines in separate threads (Ewan.Core\Runner.cs:9)
- **BaseModule<M>** - Abstract base class for pipeline modules with Init/Run/Destroy lifecycle (Ewan.Core\Module\BaseModule.cs:6)
- **MsgManager** - Thread-safe message queue system using BlockingCollection for inter-module communication (Ewan.Core\Msg\MsgManager.cs:10)

### Key Patterns
- **Singleton Pattern**: Managers use BaseManager<T> with thread-safe lazy initialization
- **Module Pipeline**: StreamRunner executes modules sequentially; failure in one module stops the pipeline
- **Message Queue**: Asynchronous message passing between components via MsgManager
- **Observer Pattern**: MsgListener/MsgSubject for event handling

### Dependencies
- **log4net** - Logging framework (version 3.1.0)
- **WPF** - Windows Presentation Foundation for UI
- **.NET Framework 4.7.2** - Target framework

### Project Structure
- **MVVM Architecture**: Main application follows Model-View-ViewModel pattern with Prism framework
- **Modular Design**: Core framework supports extensible module pipeline architecture
- **Centralized Infrastructure**: Unified logging via UILogger and messaging via MsgManager
- **Internationalization**: Complete i18n support with resource-based string management
- **Singleton Management**: Automatic manager initialization with priority-based ordering



## Coding Guidelines

### Singleton Pattern & Manager Classes

- When using singletons, always call `Instance()` **method** instead of `Instance` property.  
  ✅ Correct: `var mgr = SomeManager.Instance();`  
  ❌ Wrong: `var mgr = SomeManager.Instance;`

- **For classes requiring initialization and cleanup with singleton pattern, inherit from `BaseManager<T>`**:
  ```csharp
  using Ewan.Core;
  using Ewan.Core.Attribute;
  
  [Manager(Priority = 1)]  // Optional: for automatic initialization
  public class MyManager : BaseManager<MyManager>
  {
      public override bool Init()
      {
          // Your initialization logic here
          _uiLogger.Info(() => Ewan.Resources.LogMessages.MyManagerInitialized);
          return base.Init();
      }
      
      public override void Destroy()
      {
          // Your cleanup logic here
          _uiLogger.Info(() => Ewan.Resources.LogMessages.MyManagerDestroyed);
          base.Destroy();
      }
  }
  ```

- **Benefits of using BaseManager<T>**:
  - ✅ Thread-safe singleton implementation
  - ✅ Built-in UILogger for consistent logging
  - ✅ Automatic initialization via MainController (when using `[Manager]` attribute)
  - ✅ Standardized Init/Destroy lifecycle
  - ✅ Consistent error handling and logging

- **Manager Priority Guidelines** (used with `[Manager(Priority = n)]`):
  - **Priority 0**: Configuration classes (highest priority)
  - **Priority 1**: External connection classes (e.g., MsgManager)
  - **Priority 2**: Module startup classes
  - **Priority 99**: Other modules (default)

- **When to use BaseManager<T>**:
  - ✅ Classes that need singleton pattern
  - ✅ Classes with initialization/cleanup requirements
  - ✅ System managers and controllers
  - ✅ Service classes with state management
  
- **When NOT to use BaseManager<T>**:
  - ❌ Simple utility/helper classes without state
  - ❌ Data transfer objects (DTOs)
  - ❌ ViewModels (use Prism patterns instead)

### Message System Guidelines

- `MsgSubject` enum (including its subjects such as `UILog`) must be defined in **Ewan.Core.Msg**.  
  This ensures all modules share a consistent set of message subjects.

- The corresponding **data types / DTO classes** for each subject should be created under **Ewan.Model**.  
  For example:  
  ```csharp
  // Core
  namespace Ewan.Core.Msg
  {
      public enum MsgSubject
      {
          None,
          UILog,
      }
  }

  // Model
  namespace Ewan.Model.Messages
  {
      public class UILogMsg
      {
          public DateTime Time { get; set; }
          public string Level { get; set; }
          public string Content { get; set; }
      }
  }

### Logging Guidelines

#### UILogger Usage - The Unified Logging System

**UILogger provides dual-output logging**: immediate UI display + persistent file storage via log4net. This ensures users see logs in real-time while maintaining a complete log file for debugging and analysis.

#### Basic Usage Patterns

- **Always use expression-based resource references for type safety and internationalization**:
  ```csharp
  // ✅ Correct - Type-safe with internationalization
  _uiLogger.Info(() => Ewan.Resources.LogMessages.OperationCompleted, "DataProcessing");
  _uiLogger.Error(() => Ewan.Resources.LogMessages.ConnectionFailed, ex.Message);
  _uiLogger.Warn(() => Ewan.Resources.LogMessages.DatabaseConnectionFailed, ex.Message);
  _uiLogger.Debug(() => Ewan.Resources.LogMessages.BaseManagerInitialized, "TestManager");
  
  // ❌ Wrong - String-based, prone to errors, no internationalization
  _uiLogger.Info("OperationCompleted", "DataProcessing");
  _uiLogger.Error("Connection failed: " + ex.Message);
  ```

#### Availability in Different Contexts

- **In BaseManager<T> classes**: UILogger is automatically available via the `_uiLogger` field
  ```csharp
  public class MyManager : BaseManager<MyManager>
  {
      public override bool Init()
      {
          _uiLogger.Info(() => Ewan.Resources.LogMessages.MyManagerInitialized);
          return base.Init();
      }
  }
  ```

- **In ViewModels and other classes**: Create UILogger instance manually
  ```csharp
  public class MyViewModel : BindableBase
  {
      private readonly UILogger _uiLogger = new UILogger(typeof(Ewan.Resources.LogMessages));
      
      private void SomeMethod()
      {
          _uiLogger.Info(() => Ewan.Resources.LogMessages.ViewModelActionCompleted);
      }
  }
  ```

#### Log Levels and Usage

- **Error**: System failures, exceptions, critical issues
  ```csharp
  _uiLogger.Error(() => Ewan.Resources.LogMessages.DatabaseConnectionFailed, ex.Message);
  ```

- **Warn**: Non-critical issues, deprecated usage, configuration problems
  ```csharp
  _uiLogger.Warn(() => Ewan.Resources.LogMessages.Log4netConfigNotFound);
  ```

- **Info**: Normal application flow, successful operations, status updates
  ```csharp
  _uiLogger.Info(() => Ewan.Resources.LogMessages.SystemInitialized);
  _uiLogger.Info(() => Ewan.Resources.LogMessages.OperationCompleted, operation.Name);
  ```

- **Debug**: Detailed diagnostic information (usually disabled in production)
  ```csharp
  _uiLogger.Debug(() => Ewan.Resources.LogMessages.BaseManagerInitialized, "DatabaseManager");
  ```

#### Resource Management Best Practices

1. **Add all log messages to Ewan.Resources**:
   - Create entries in `LogMessages.resx` (English)
   - Create corresponding entries in `LogMessages.zh-CN.resx` (Chinese)
   - Regenerate `LogMessages.Designer.cs` to get strongly-typed properties

2. **Use parameterized messages for dynamic content**:
   ```csharp
   // Resource: "Database connection failed: {0}"
   _uiLogger.Error(() => Ewan.Resources.LogMessages.DatabaseConnectionFailed, ex.Message);
   
   // Resource: "Processing completed in {0} seconds"
   _uiLogger.Info(() => Ewan.Resources.LogMessages.ProcessingComplete, elapsed.TotalSeconds.ToString("F2"));
   ```

#### DO NOT Use Direct log4net

- **❌ Never use log4net directly** - Always use UILogger for consistency
  ```csharp
  // ❌ Wrong - bypasses UI display and internationalization
  private static readonly ILog _log = LogManager.GetLogger(typeof(MyClass));
  _log.Info("Some message");
  
  // ✅ Correct - unified logging with UI display
  _uiLogger.Info(() => Ewan.Resources.LogMessages.SomeMessage);
  ```

#### UILogger Architecture

```
Business Logic → UILogger → [UI Display + log4net File]
                     ↓              ↓
               MsgManager    Logs/app{date}.txt
                     ↓
            LogWindow (Real-time display)
```

**Benefits**:
- ✅ Real-time UI feedback for users
- ✅ Complete file-based logging for debugging
- ✅ Internationalization support
- ✅ Type-safe message references
- ✅ Consistent formatting and structure
- ✅ Centralized logging configuration

### Internationalization (i18n) Guidelines

#### Resource File Organization

All UI strings and user-facing messages must be stored in `.resx` resource files in the **Ewan.Resources** project:

```
Ewan.Resources/
├── LogMessages.resx             # Log messages (English - default)
├── LogMessages.zh-CN.resx       # Log messages (Chinese)
├── UIStrings.resx              # UI strings (English - default)
├── UIStrings.zh-CN.resx        # UI strings (Chinese)
├── UIStrings.en-US.resx        # UI strings (US English variant)
├── PermissionConfigStrings.resx     # Permission config UI (English)
├── PermissionConfigStrings.zh-CN.resx # Permission config UI (Chinese)
└── [Module]Strings.resx        # Module-specific strings
```

#### Resource File Standards

1. **Always use .resx files, NOT XAML ResourceDictionary**:
   ```csharp
   // ✅ Correct - Use .resx resources with Designer.cs
   WindowTitle = Ewan.Resources.UIStrings.MainWindowTitle;
   
   // ❌ Wrong - XAML ResourceDictionary
   <ResourceDictionary Source="/Resources/Strings.xaml"/>
   ```

2. **Resource file configuration in .csproj**:
   ```xml
   <EmbeddedResource Include="UIStrings.resx">
     <Generator>PublicResXFileCodeGenerator</Generator>
     <LastGenOutput>UIStrings.Designer.cs</LastGenOutput>
     <SubType>Designer</SubType>
   </EmbeddedResource>
   ```

3. **Naming conventions**:
   - **LogMessages.resx**: System log messages and operational messages
   - **UIStrings.resx**: General UI text (menus, buttons, labels)
   - **[Feature]Strings.resx**: Feature-specific UI strings (e.g., PermissionConfigStrings.resx)

#### Culture Management

1. **Set Culture through CultureManager**:
   ```csharp
   // In ViewModel constructor or initialization
   _cultureManager = CultureManager.Instance();
   _cultureManager.CultureChanged += OnCultureChanged;
   
   // Update resource culture when language changes
   private void OnCultureChanged(object sender, CultureChangedEventArgs e)
   {
       Ewan.Resources.UIStrings.Culture = e.NewCulture;
       Ewan.Resources.LogMessages.Culture = e.NewCulture;
       UpdateUITexts(); // Refresh all UI strings
   }
   ```

2. **Synchronize Culture in ViewModels**:
   ```csharp
   private void UpdateUITexts()
   {
       // Set culture before accessing resources
       Ewan.Resources.UIStrings.Culture = _cultureManager.CurrentCulture;
       
       // Update all bound properties
       WindowTitle = Ewan.Resources.UIStrings.WindowTitle;
       SaveButtonText = Ewan.Resources.UIStrings.SaveButton;
       
       // Force UI refresh
       RaisePropertyChanged(nameof(WindowTitle));
       RaisePropertyChanged(nameof(SaveButtonText));
   }
   ```

#### Using Resources in Code

1. **In ViewModels (MVVM pattern)**:
   ```csharp
   public class MyViewModel : BindableBase
   {
       private string _title;
       
       public string Title
       {
           get => _title;
           set => SetProperty(ref _title, value);
       }
       
       private void UpdateUITexts()
       {
           Ewan.Resources.UIStrings.Culture = _cultureManager.CurrentCulture;
           Title = Ewan.Resources.UIStrings.MyWindowTitle;
       }
   }
   ```

2. **In XAML (through binding)**:
   ```xml
   <!-- Bind to ViewModel properties, NOT directly to resources -->
   <Window Title="{Binding WindowTitle}">
       <Button Content="{Binding SaveButtonText}" />
   </Window>
   ```

3. **For dynamic/formatted strings**:
   ```csharp
   // Resource: "Processing {0} items..."
   var message = string.Format(
       Ewan.Resources.UIStrings.ProcessingItems, 
       itemCount
   );
   ```

#### Adding New Resources

1. **Step 1: Add to default resource file (.resx)**:
   - Open `UIStrings.resx` in Visual Studio
   - Add new entry with Name and Value
   - Save the file

2. **Step 2: Add translations to culture-specific files**:
   - Open `UIStrings.zh-CN.resx`
   - Add same Name with translated Value
   - Repeat for other cultures

3. **Step 3: Regenerate Designer.cs** (happens automatically in Visual Studio):
   - The Designer.cs file will auto-generate with strongly-typed properties
   - If not, right-click .resx file → Run Custom Tool

4. **Step 4: Use in code**:
   ```csharp
   var text = Ewan.Resources.UIStrings.NewResourceKey;
   ```

#### Common Pitfalls to Avoid

1. **❌ Don't hardcode strings in ViewModels**:
   ```csharp
   // Wrong
   WindowTitle = "权限配置";
   WindowTitle = isChinese ? "权限配置" : "Permission Configuration";
   ```

2. **❌ Don't use XAML ResourceDictionary for i18n**:
   ```xml
   <!-- Wrong -->
   <ResourceDictionary Source="/Resources/Strings.xaml"/>
   ```

3. **❌ Don't forget to escape special XML characters in .resx**:
   ```xml
   <!-- Wrong -->
   <data name="BackupRestore">
     <value>Backup & Restore</value>
   </data>
   
   <!-- Correct -->
   <data name="BackupRestore">
     <value>Backup &amp; Restore</value>
   </data>
   ```

4. **❌ Don't access resources without setting Culture**:
   ```csharp
   // Wrong - uses default culture
   var text = Ewan.Resources.UIStrings.SomeText;
   
   // Correct - uses current culture
   Ewan.Resources.UIStrings.Culture = _cultureManager.CurrentCulture;
   var text = Ewan.Resources.UIStrings.SomeText;
   ```

#### Best Practices

1. **Always group related strings together** in resource files
2. **Use descriptive resource keys** that indicate usage:
   - `MainWindowTitle` instead of `Title1`
   - `SaveButtonText` instead of `Button1`
3. **Include placeholders** for dynamic content:
   - `"Processing {0} of {1} items..."` instead of hardcoding
4. **Test all languages** after adding new resources
5. **Keep translations synchronized** - missing translations fall back to default
6. **Use same key names** across all culture-specific files