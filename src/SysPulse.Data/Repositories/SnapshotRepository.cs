using Microsoft.EntityFrameworkCore;
using SysPulse.Data.Context;
using SysPulse.Data.Entities;

namespace SysPulse.Data.Repositories;

public class SnapshotRepository : ISnapshotRepository{
    private readonly SysPulseDbContext _sysPulseDbContext;

    public SnapshotRepository(SysPulseDbContext sysPulseDbContext){
        _sysPulseDbContext=sysPulseDbContext;
    }

    public async Task EnsureDatabaseCreatedAsync(CancellationToken ct=default){
        // EnsureCreatedAsync can throw DbException or SocketException if the database provider connection fails
        await _sysPulseDbContext.Database.EnsureCreatedAsync(ct);
        try{
            // Auto-migrate: ensure Hostname column exists in existing database schemas
            if(_sysPulseDbContext.Database.IsSqlite()){
                await _sysPulseDbContext.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Snapshots\" ADD COLUMN \"Hostname\" TEXT NOT NULL DEFAULT 'localhost';", ct);
            }else{
                await _sysPulseDbContext.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE `Snapshots` ADD COLUMN `Hostname` VARCHAR(128) NOT NULL DEFAULT 'localhost';", ct);
            }
        }
        catch{
            // Column already exists or table was freshly created
        }
    }

    public async Task SaveSnapshotAsync(SystemSnapshot snapshot, CancellationToken ct=default){
        // SaveChangesAsync can throw DbUpdateException if constraints fail, or DbException / TimeoutException
        _sysPulseDbContext.Snapshots.Add(snapshot);
        await _sysPulseDbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SystemSnapshot>> GetRecentSnapshotsAsync(int count=60, CancellationToken ct=default){
        // ToListAsync can throw DbException or OperationCanceledException
        return await _sysPulseDbContext.Snapshots
            .OrderByDescending(s => s.TimestampUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SystemSnapshot>> GetSnapshotsAsync(string? hostname=null, int count=100, CancellationToken ct=default){
        // Querying snapshots with optional hostname filter
        IQueryable<SystemSnapshot> query=_sysPulseDbContext.Snapshots.AsQueryable();
        if(!string.IsNullOrWhiteSpace(hostname)){
            query=query.Where(s => s.Hostname == hostname.Trim());
        }
        return await query
            .OrderByDescending(s => s.TimestampUtc)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteSnapshotAsync(int id, CancellationToken ct=default){
        // FindAsync and SaveChangesAsync can throw DbException
        SystemSnapshot? entity=await _sysPulseDbContext.Snapshots.FindAsync([id], ct);
        if(entity is null){
            return false;
        }
        _sysPulseDbContext.Snapshots.Remove(entity);
        await _sysPulseDbContext.SaveChangesAsync(ct);
        return true;
    }
}
