using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SysPulse.Data.Context;
using SysPulse.Data.Entities;
using SysPulse.Data.Repositories;
using Xunit;

namespace SysPulse.Tests;

public class SnapshotRepositoryTests : IDisposable{
    private readonly SqliteConnection _sqliteConnection;
    private readonly SysPulseDbContext _sysPulseDbContext;
    private readonly SnapshotRepository _snapshotRepository;

    public SnapshotRepositoryTests(){
        _sqliteConnection=new SqliteConnection("DataSource=:memory:");
        _sqliteConnection.Open();

        DbContextOptions<SysPulseDbContext> options=new DbContextOptionsBuilder<SysPulseDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _sysPulseDbContext=new SysPulseDbContext(options);
        _sysPulseDbContext.Database.EnsureCreated();

        _snapshotRepository=new SnapshotRepository(_sysPulseDbContext);
    }

    [Fact]
    public async Task SaveSnapshotAsync_PersistsAndReadsSnapshot(){
        SystemSnapshot snapshot=new SystemSnapshot();
        snapshot.TimestampUtc=DateTime.UtcNow;
        snapshot.CpuUsagePercent=25.5;
        snapshot.MemoryUsagePercent=42.0;
        snapshot.MemoryUsedGb=6.5;
        snapshot.MemoryTotalGb=16.0;
        snapshot.SwapUsagePercent=0.0;
        snapshot.DiskUsagePercent=55.2;
        snapshot.NetworkDownloadKbps=105.4;
        snapshot.NetworkUploadKbps=24.1;
        snapshot.Note="Test Snapshot";

        await _snapshotRepository.SaveSnapshotAsync(snapshot);

        IReadOnlyList<SystemSnapshot> recent=await _snapshotRepository.GetRecentSnapshotsAsync(10);
        Assert.Single(recent);
        Assert.Equal(25.5, recent[0].CpuUsagePercent);
        Assert.Equal("Test Snapshot", recent[0].Note);
    }

    [Fact]
    public async Task GetRecentSnapshotsAsync_OrdersByDescendingTimestamp(){
        for(int i=1;i<=5;i++){
            SystemSnapshot snapshot=new SystemSnapshot();
            snapshot.TimestampUtc=DateTime.UtcNow.AddMinutes(i);
            snapshot.CpuUsagePercent=i * 10;
            snapshot.Note=$"Snap #{i}";
            await _snapshotRepository.SaveSnapshotAsync(snapshot);
        }

        IReadOnlyList<SystemSnapshot> list=await _snapshotRepository.GetRecentSnapshotsAsync(3);
        Assert.Equal(3, list.Count);
        Assert.Equal("Snap #5", list[0].Note);
        Assert.Equal("Snap #4", list[1].Note);
        Assert.Equal("Snap #3", list[2].Note);
    }

    public void Dispose(){
        _sysPulseDbContext.Dispose();
        _sqliteConnection.Dispose();
    }
}
