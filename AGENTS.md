# SysPulse Development Guidelines & Architecture Rules

## 1. UI Modularization & UserControl Views
- **Never dump everything into one Window**: Never place full tab implementations, complex screen layouts, or modals directly in `MainWindow.axaml` or any other top-level window.
- **Modular UserControls**: Always decompose distinct screens, submenus, tabs, and modal overlays into dedicated `UserControl`s located in `Views/` (e.g. `DashboardView`, `SnapshotsView`, `SettingsView`, `DeleteAllConfirmationModal`).
- **Shell Windows**: Top-level `Window`s must act strictly as lightweight host shells responsible for the window frame, global navigation, and hosting child `UserControl`s.
- **Shared DataContext**: Child `UserControl`s inherit the `DataContext` from their host window or view container unless a scoped ViewModel is explicitly designated.

## 2. ViewModel Modularization
- Split feature-rich ViewModels into partial class files (e.g., `MainViewModel.cs`, `MainViewModel.Snapshots.cs`, `MainViewModel.Settings.cs`) to keep each sub-feature manageable and readable.

## 3. C# Code Formatting & Style Constraints
- **Zero spaces around `=`**: Always write `int i=0;`, `property=value;`, `Type obj=new Type();`.
- **Attached `{` with NO preceding space**: Always write `if(condition){`, `void Method(){`, `public class Foo{`.
- **Statements and `}` on separate newlines**: Never write single-line blocks.
- **`catch` formatting**: Always put `catch` on its own line:
  ```csharp
  }
  catch(Exception ex){
  ```
- **No blank lines inside method bodies**: Method bodies must not contain empty whitespace lines.
- **Strong typing (NO `var`)**: All variable types must be explicitly stated.
- **No `foreach` loops**: Use standard indexed `for(int i=0;i<count;i++){` loops instead.
- **Field Naming**: Private fields must start with `_` and use camelCase (e.g., `_sysPulseDbContext`). Public properties must use PascalCase.
- **No emojis or non-ASCII characters**: Zero emojis anywhere in XAML or C# source files.
