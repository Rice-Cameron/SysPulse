using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.EntityFrameworkCore.Extensions;
using SysPulse.Core.Services;
using SysPulse.Data.Context;
using SysPulse.Data.Repositories;
using SysPulse.Desktop.ViewModels;
using SysPulse.Desktop.Views;

namespace SysPulse.Desktop;

public partial class App : Application{
    public IServiceProvider? Services{get;private set;}

    public override void Initialize(){
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted(){
        ServiceCollection services=new ServiceCollection();
        // 1. Load Configuration
        IConfigurationRoot config=new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();
        services.AddSingleton<IConfiguration>(config);
        // 2. Register Linux Metrics Service
        services.AddSingleton<ILinuxMetricCollector, LinuxMetricCollector>();
        // 3. Register EF Core Database (MySQL/MariaDB with automatic SQLite fallback)
        string provider=Environment.GetEnvironmentVariable("SYSPULSE_DB_PROVIDER") ?? config["DatabaseProvider"] ?? "SQLite";
        if(provider.Equals("MySQL", StringComparison.OrdinalIgnoreCase)){
            string? connStr=Environment.GetEnvironmentVariable("SYSPULSE_MYSQL_CONN") ?? config.GetConnectionString("MySQL");
            services.AddDbContext<SysPulseDbContext>(options =>
                options.UseMySQL(connStr ?? "Server=localhost;Port=3306;Database=syspulse;User=root;"));
        }else{
            string localDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SysPulse");
            Directory.CreateDirectory(localDir);
            string dbPath=Path.Combine(localDir, "syspulse.db");
            services.AddDbContext<SysPulseDbContext>(options =>
                options.UseSqlite($"Data Source={dbPath}"));
        }
        // 4. Register Repositories and ViewModels
        services.AddTransient<ISnapshotRepository, SnapshotRepository>();
        services.AddSingleton<MainViewModel>();
        Services=services.BuildServiceProvider();
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){
            MainViewModel mainViewModel=Services.GetRequiredService<MainViewModel>();
            mainViewModel.DatabaseProviderName=provider.ToUpperInvariant();
            MainWindow mainWindow=new MainWindow();
            mainWindow.DataContext=mainViewModel;
            desktop.MainWindow=mainWindow;
        }
        base.OnFrameworkInitializationCompleted();
    }
}