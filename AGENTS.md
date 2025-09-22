# Repository Guidelines

## Project Structure & Module Organization
MarkingMachineFeeder.sln ties together the WPF client and supporting libraries. The `MarkingMachineFeeder/` app hosts views in `Window/`, view models in `Viewmodel/`, and user controls in `UserControls/`. Shared automation logic resides in `Ewan.Core/`, while domain objects live in `Ewan.Model/` and hardware orchestration under `Ewan.BusinessBonding/`. UI components and themes are in `Ewan.Controls/`, and localization plus generated designers are under `Ewan.Resources/`. Vendor drivers and wiring diagrams stay in `Hardware/`, and legacy packages resolve from `packages/`.

## Build, Test, and Development Commands
- `nuget restore MarkingMachineFeeder.sln` - hydrate `packages.config` dependencies before the first build.
- `msbuild MarkingMachineFeeder.sln /p:Configuration=Debug` - compile all projects for interactive debugging.
- `msbuild MarkingMachineFeeder.sln /t:Rebuild /p:Configuration=Release` - clean and produce a deployable release drop.
- `MarkingMachineFeeder/bin/Debug/MarkingMachineFeeder.exe` - launch the HMI locally; attach to the vendor simulator when the PLC is unavailable.

## Coding Style & Naming Conventions
Follow Visual Studio C# defaults: 4-space indentation, braces on new lines, and nullable annotations when available. Use PascalCase for classes, view models, and public members; camelCase for locals and private fields, preferring `_field` for backing stores. Keep XAML filenames aligned with their code-behind (for example `Window/MainWindow.xaml` plus `.cs`). Resource keys in `Ewan.Resources` stay PascalCase and should ship with zh-CN companions for user-facing strings.

## Testing Guidelines
There is no automated test project yet, so smoke test the WPF shell through the primary workflows in `Window/` before pushing. When adding unit coverage, create a sibling `<ProjectName>.Tests` project (MSTest or NUnit) and name files `<Feature>Tests.cs`. Record manual validation steps in the PR, especially when motion control or IO bindings are affected. Keep any hardware simulators or mock drivers under `Hardware/` (create a `Simulators` subfolder if needed) so they can be swapped in during CI.

## Commit & Pull Request Guidelines
History favors concise Chinese imperative summaries (for example `添加安全io监控`); continue that format or provide an equivalent localized verb-first line. Keep commits logically scoped and mention the subsystem (Core, Model, UI) when it clarifies the change. Pull requests should include a short summary, screenshots for UI updates, linked work items, and explicit hardware prerequisites. Request at least one reviewer familiar with the touched module and confirm a clean build before assigning reviewers.

## Localization & Configuration Tips
Update `.resx` pairs in `Ewan.Resources/` together and regenerate designer files via Visual Studio to avoid manual edits. Store environment-specific machine settings outside source control; use the samples under `Ewan.Model/Config` as the starting point for new stations.
