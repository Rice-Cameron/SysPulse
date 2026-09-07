using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SysPulse.Data.Context;
using SysPulse.Data.Entities;
using SysPulse.Data.Repositories;
using Xunit;

namespace SysPulse.Tests;

public class SnapshotRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SysPulseDbContext _dbContext;
    private readonly SnapshotRepository _repository;

    public SnapshotRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SysPulseDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new SysPulseDbContext(options);
        _dbContext.Database.EnsureCreated();

        _repository = new SnapshotRepository(_dbContext);
    }

    [Fact]
    public async Task SaveSnapshotAsync_PersistsAndReadsSnapshot()
    {
        var snapshot = new SystemSnapshot
        {
            TimestampUtc = DateTime.UtcNow,
            CpuUsagePercent = 25.5,
            MemoryUsagePercent = 42.0,
            MemoryUsedGb = 6.5,
            MemoryTotalGb = 16.0,
            SwapUsagePercent = 0.0,
            DiskUsagePercent = 55.2,
            NetworkDownloadKbps = 105.4,
            NetworkUploadKbps = 24.1,
            Note = "Test Snapshot"
        };

        await _repository.SaveSnapshotAsync(snapshot);

        var recent = await _repository.GetRecentSnapshotsAsync(10);
        Assert.Single(recent);
        Assert.Equal(25.5, recent[0].CpuUsagePercent);
        Assert.Equal("Test Snapshot", recent[0].Note);
    }

    [Fact]
    public async Task GetRecentSnapshotsAsync_OrdersByDescendingTimestamp()
    {
        for (int i = 1; i <= 5; i++)
        {
            await _repository.SaveSnapshotAsync(new SystemSnapshot
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(i),
                CpuUsagePercent = i * 10,
                Note = $"Snap #{i}"
            });
        }

        var list = await _repository.GetRecentSnapshotsAsync(3);
        Assert.Equal(3, list.Count);
        Assert.Equal("Snap #5", list[0].Note);
        Assert.Equal("Snap #4", list[1].Note);
        Assert.Equal("Snap #3", list[2].Note);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
