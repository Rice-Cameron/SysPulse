# SysPulse — Native Linux Desktop Command Center

SysPulse is a cross-platform, glanceable system monitor and command center built with **Avalonia UI** (C# / XAML / MVVM), modern **.NET**, and **Entity Framework Core** with **MySQL/MariaDB** (and local SQLite) support.

Unlike terminal tools like `btop` which can be dense and difficult to parse at a glance, SysPulse provides high-contrast, color-coded visual cards for CPU, RAM, Swap, Drives, Network throughput, and top active processes.

---

## Architecture Overview

```
SysPulse/
├── SysPulse.slnx                 (Modern lightweight solution file)
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
