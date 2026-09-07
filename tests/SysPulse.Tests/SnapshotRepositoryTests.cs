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
        snapshot.Hostname="server-01";
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
        Assert.Equal("server-01", recent[0].Hostname);
        Assert.Equal("Test Snapshot", recent[0].Note);
    }

    [Fact]
    public async Task GetRecentSnapshotsAsync_OrdersByDescendingTimestamp(){
        for(int i=1;i<=5;i++){
            SystemSnapshot snapshot=new SystemSnapshot();
            snapshot.Hostname=$"node-0{i}";
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

    [Fact]
    public async Task GetSnapshotsAsync_FiltersByHostname(){
        SystemSnapshot s1=new SystemSnapshot();
        s1.Hostname="alpha-server";
        s1.CpuUsagePercent=10.0;
        s1.Note="Alpha 1";
        await _snapshotRepository.SaveSnapshotAsync(s1);
        SystemSnapshot s2=new SystemSnapshot();
        s2.Hostname="beta-server";
        s2.CpuUsagePercent=20.0;
        s2.Note="Beta 1";
        await _snapshotRepository.SaveSnapshotAsync(s2);
        IReadOnlyList<SystemSnapshot> alphaList=await _snapshotRepository.GetSnapshotsAsync("alpha-server", 10);
        Assert.Single(alphaList);
        Assert.Equal("Alpha 1", alphaList[0].Note);
        IReadOnlyList<SystemSnapshot> allList=await _snapshotRepository.GetSnapshotsAsync(null, 10);
        Assert.True(allList.Count >= 2);
    }

    [Fact]
    public async Task DeleteSnapshotAsync_RemovesSnapshot(){
        SystemSnapshot snapshot=new SystemSnapshot();
        snapshot.Hostname="temp-server";
        snapshot.Note="To Delete";
        await _snapshotRepository.SaveSnapshotAsync(snapshot);
        IReadOnlyList<SystemSnapshot> list=await _snapshotRepository.GetSnapshotsAsync("temp-server", 10);
        Assert.Single(list);
        int id=list[0].Id;
        bool deleted=await _snapshotRepository.DeleteSnapshotAsync(id);
        Assert.True(deleted);
        IReadOnlyList<SystemSnapshot> afterDelete=await _snapshotRepository.GetSnapshotsAsync("temp-server", 10);
        Assert.Empty(afterDelete);
    }

    [Fact]
    public async Task DeleteAllSnapshotsAsync_DeletesAllRecords(){
        SystemSnapshot s1=new SystemSnapshot();
        s1.Hostname="host-a";
        await _snapshotRepository.SaveSnapshotAsync(s1);
        SystemSnapshot s2=new SystemSnapshot();
        s2.Hostname="host-b";
        await _snapshotRepository.SaveSnapshotAsync(s2);
        int deletedCount=await _snapshotRepository.DeleteAllSnapshotsAsync();
        Assert.True(deletedCount >= 2);
        IReadOnlyList<SystemSnapshot> remaining=await _snapshotRepository.GetSnapshotsAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task GetSnapshotCountAsync_ReturnsAccurateCount(){
        int initialCount=await _snapshotRepository.GetSnapshotCountAsync();
        Assert.Equal(0, initialCount);
        SystemSnapshot s1=new SystemSnapshot();
        s1.Hostname="count-host";
        await _snapshotRepository.SaveSnapshotAsync(s1);
        SystemSnapshot s2=new SystemSnapshot();
        s2.Hostname="count-host";
        await _snapshotRepository.SaveSnapshotAsync(s2);
        int afterCount=await _snapshotRepository.GetSnapshotCountAsync();
        Assert.Equal(2, afterCount);
    }

    public void Dispose(){
        _sysPulseDbContext.Dispose();
        _sqliteConnection.Dispose();
    }
}

