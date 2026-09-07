# SysPulse — Linux Desktop System Monitor

SysPulse is a hands-on learning project built to explore modern cross-platform **.NET**, **Avalonia UI** (C# / XAML / MVVM), and **Entity Framework Core** on Linux.

Coming from a background working primarily with legacy Windows .NET (WPF, WinForms, older .NET Framework), this repository serves as a personal sandbox to experience how modern, open-source .NET development feels natively on a Linux desktop—from reading `/proc` and `sysfs` kernel telemetry directly to building responsive XAML dashboards and working entirely from the command line without Visual Studio.

---

## What This Project Explores

- **Modern .NET & C# on Linux**: Working directly from the terminal with the free `dotnet` CLI (`dotnet build`, `dotnet run`, `dotnet watch`), modern SDK-style projects, and CommunityToolkit MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`).
- **Avalonia UI**: Modern cross-platform XAML rendering natively on Linux desktop environments with responsive cards, theme switching (Dark & Light mode), and compile-time data bindings.
- **Native Linux Telemetry**: Reading performance metrics directly from the Linux virtual filesystems (`/proc/stat`, `/proc/meminfo`, `/proc/net/dev`, and `/sys/devices/system/cpu/...`) with zero external native wrapper dependencies.
- **EF Core Persistence**: Code-first database persistence featuring zero-config local SQLite fallback alongside MySQL / MariaDB support for saving and archiving system snapshots.
- **Automated Testing**: Unit and integration testing metric parsers and database repository flows with xUnit.

---

## Architecture Overview

```
SysPulse/
├── SysPulse.sln                  (Lightweight solution file)
├── .gitignore                    (Ignores bin/, obj/, and local DBs)
├── README.md                     (Architecture and guide)
├── src/
│   ├── SysPulse.Core/            (Class Library: Linux metric collection & models)
│   │   ├── Models/               (CpuMetrics, MemoryMetrics, DriveMetrics, etc.)
│   │   └── Services/             (LinuxMetricCollector parsing /proc and sysfs)
│   ├── SysPulse.Data/            (Class Library: EF Core & persistence)
│   │   ├── Context/              (SysPulseDbContext supporting MySQL & SQLite)
│   │   ├── Entities/             (SystemSnapshot entity)
│   │   └── Repositories/         (SnapshotRepository for saving/querying)
│   └── SysPulse.Desktop/         (Avalonia UI Native Executable)
│       ├── ViewModels/           (MainViewModel, DriveItem, NetworkItem, ProcessItem)
│       ├── Views/                (MainWindow.axaml glanceable dashboard)
│       ├── appsettings.json      (Database & polling configuration)
│       ├── App.axaml / App.axaml.cs (Modern Dependency Injection setup)
│       └── Program.cs            (Avalonia application bootstrapper)
└── tests/
    └── SysPulse.Tests/           (xUnit automated unit & integration tests)
```

---

## Quick Start: Running & Building Without Visual Studio

You do **not** need Visual Studio or any paid IDE. Modern .NET includes the official C# compiler (`Roslyn`) and build system (`MSBuild`) directly inside the free `dotnet` CLI.

### 1. Run the Desktop Application
```bash
cd ~/Documents/github/SysPulse
dotnet run --project src/SysPulse.Desktop
```

### 2. Run with Hot Reload (`dotnet watch`)
As you edit `.axaml` or `.cs` files in your text editor, `dotnet watch` automatically applies changes on the fly without needing to stop and restart the app:
```bash
dotnet watch --project src/SysPulse.Desktop
```

### 3. Build the Solution
```bash
dotnet build
```

### 4. Run Automated Unit Tests
```bash
dotnet test
```

---

## Free Code Editors on Linux

You already have free editors installed on your system:

### Visual Studio Code
1. Open the project:
   ```bash
   code ~/Documents/github/SysPulse
   ```
2. Recommended Free Extensions:
   - **C# Dev Kit** or **C#** (by Microsoft): Syntax highlighting, code navigation, IntelliSense, debugging.
   - **Avalonia for VSCode** (by AvaloniaUI): Live XAML previewer and XAML autocomplete.

### Neovim
```bash
nvim ~/Documents/github/SysPulse
```
Works out-of-the-box with Omnisharp or Roslyn LSP for C# completion.

---

## Database Configuration (MySQL / MariaDB vs SQLite)

SysPulse includes zero-config fallback to **SQLite** so the application runs immediately without requiring a running database server.

To switch to **MySQL** or **MariaDB**:
1. Open `src/SysPulse.Desktop/appsettings.json`:
   ```json
   {
     "DatabaseProvider": "MySQL",
     "ConnectionStrings": {
       "MySQL": "Server=localhost;Port=3306;Database=syspulse;User=your_user;Password=your_password;"
     }
   }
   ```
2. EF Core will automatically connect to your MariaDB/MySQL server, create the tables if they don't exist (`EnsureCreatedAsync`), and record snapshots.

---

## Appendix: How This Solution Was Bootstrapped (CLI Reference)

> [!NOTE]
> **You do not need to run these commands if you cloned this repository.** All project files, solution configurations, and package dependencies are already committed into Git. Simply run `dotnet run --project src/SysPulse.Desktop` or `dotnet build`.

The commands below are documented purely for reference to illustrate how a multi-project .NET, Avalonia, and EF Core solution is constructed from scratch using only the `dotnet` CLI:

```bash
# 1. Create directory and modern solution
mkdir -p ~/Documents/github/SysPulse/src
cd ~/Documents/github/SysPulse
dotnet new sln -n SysPulse

# 2. Create the projects
dotnet new classlib -o src/SysPulse.Core -n SysPulse.Core
dotnet new classlib -o src/SysPulse.Data -n SysPulse.Data
dotnet new avalonia.mvvm -o src/SysPulse.Desktop -n SysPulse.Desktop
dotnet new xunit -o tests/SysPulse.Tests -n SysPulse.Tests

# 3. Add all projects to the solution
dotnet sln add src/SysPulse.Core/SysPulse.Core.csproj
dotnet sln add src/SysPulse.Data/SysPulse.Data.csproj
dotnet sln add src/SysPulse.Desktop/SysPulse.Desktop.csproj
dotnet sln add tests/SysPulse.Tests/SysPulse.Tests.csproj

# 4. Wire up project references (Clean Dependency Flow)
dotnet add src/SysPulse.Data/SysPulse.Data.csproj reference src/SysPulse.Core/SysPulse.Core.csproj
dotnet add src/SysPulse.Desktop/SysPulse.Desktop.csproj reference src/SysPulse.Core/SysPulse.Core.csproj
dotnet add src/SysPulse.Desktop/SysPulse.Desktop.csproj reference src/SysPulse.Data/SysPulse.Data.csproj
dotnet add tests/SysPulse.Tests/SysPulse.Tests.csproj reference src/SysPulse.Core/SysPulse.Core.csproj
dotnet add tests/SysPulse.Tests/SysPulse.Tests.csproj reference src/SysPulse.Data/SysPulse.Data.csproj

# 5. Add NuGet packages
dotnet add src/SysPulse.Core/SysPulse.Core.csproj package CommunityToolkit.Mvvm
dotnet add src/SysPulse.Data/SysPulse.Data.csproj package MySql.EntityFrameworkCore
dotnet add src/SysPulse.Data/SysPulse.Data.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/SysPulse.Data/SysPulse.Data.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src/SysPulse.Desktop/SysPulse.Desktop.csproj package Microsoft.Extensions.DependencyInjection
dotnet add src/SysPulse.Desktop/SysPulse.Desktop.csproj package Microsoft.Extensions.Configuration.Json
dotnet add tests/SysPulse.Tests/SysPulse.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite
```
