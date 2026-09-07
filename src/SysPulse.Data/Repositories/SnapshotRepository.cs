using Microsoft.EntityFrameworkCore;
using SysPulse.Data.Context;
using SysPulse.Data.Entities;

namespace SysPulse.Data.Repositories;

public class SnapshotRepository : ISnapshotRepository {
    private readonly SysPulseDbContext _sysPulseDbContext;

    public SnapshotRepository(SysPulseDbContext sysPulseDbContext) {
        _sysPulseDbContext = sysPulseDbContext;
    }

    public async Task EnsureDatabaseCreatedAsync(CancellationToken ct = default) {
        // EnsureCreatedAsync can throw DbException or SocketException if the database provider connection fails
        await _sysPulseDbContext.Database.EnsureCreatedAsync(ct);
    }

    public async Task SaveSnapshotAsync(SystemSnapshot snapshot, CancellationToken ct = default) {
        // SaveChangesAsync can throw DbUpdateException if constraints fail, or DbException / TimeoutException
        _sysPulseDbContext.Snapshots.Add(snapshot);
        await _sysPulseDbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SystemSnapshot>> GetRecentSnapshotsAsync(int count = 60, CancellationToken ct = default) {
        // ToListAsync can throw DbException or OperationCanceledException
        return await _sysPulseDbContext.Snapshots
            .OrderByDescending(s => s.TimestampUtc)
            .Take(count)
            .ToListAsync(ct);
    }
}
