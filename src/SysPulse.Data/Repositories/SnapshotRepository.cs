using Microsoft.EntityFrameworkCore;
using SysPulse.Data.Context;
using SysPulse.Data.Entities;

namespace SysPulse.Data.Repositories;

public class SnapshotRepository : ISnapshotRepository
{
    private readonly SysPulseDbContext _dbContext;

    public SnapshotRepository(SysPulseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureDatabaseCreatedAsync(CancellationToken ct = default)
    {
        await _dbContext.Database.EnsureCreatedAsync(ct);
    }

    public async Task SaveSnapshotAsync(SystemSnapshot snapshot, CancellationToken ct = default)
    {
        _dbContext.Snapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SystemSnapshot>> GetRecentSnapshotsAsync(int count = 60, CancellationToken ct = default)
    {
        return await _dbContext.Snapshots
            .OrderByDescending(s => s.TimestampUtc)
            .Take(count)
            .ToListAsync(ct);
    }
}
