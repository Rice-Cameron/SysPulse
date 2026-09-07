using System.Threading;
using Microsoft.EntityFrameworkCore;
using SysPulse.Data.Context;
using SysPulse.Data.Entities;

namespace SysPulse.Data.Repositories;

public class SnapshotRepository : ISnapshotRepository{
    private readonly SysPulseDbContext _sysPulseDbContext;
    private readonly SemaphoreSlim _semaphore=new SemaphoreSlim(1, 1);

    public SnapshotRepository(SysPulseDbContext sysPulseDbContext){
        _sysPulseDbContext=sysPulseDbContext;
    }

    public async Task EnsureDatabaseCreatedAsync(CancellationToken ct=default){
        await _semaphore.WaitAsync(ct);
        try{
            await _sysPulseDbContext.Database.EnsureCreatedAsync(ct);
            try{
                if(_sysPulseDbContext.Database.IsSqlite()){
                    await _sysPulseDbContext.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE \"Snapshots\" ADD COLUMN \"Hostname\" TEXT NOT NULL DEFAULT 'localhost';", ct);
                }else{
                    await _sysPulseDbContext.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE `Snapshots` ADD COLUMN `Hostname` VARCHAR(128) NOT NULL DEFAULT 'localhost';", ct);
                }
            }
            catch{
            }
        }
        finally{
            _semaphore.Release();
        }
    }

    public async Task SaveSnapshotAsync(SystemSnapshot snapshot, CancellationToken ct=default){
        await _semaphore.WaitAsync(ct);
        try{
            _sysPulseDbContext.Snapshots.Add(snapshot);
            await _sysPulseDbContext.SaveChangesAsync(ct);
        }
        finally{
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<SystemSnapshot>> GetRecentSnapshotsAsync(int count=60, CancellationToken ct=default){
        await _semaphore.WaitAsync(ct);
        try{
            return await _sysPulseDbContext.Snapshots
                .OrderByDescending(s => s.TimestampUtc)
                .Take(count)
                .ToListAsync(ct);
        }
        finally{
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<SystemSnapshot>> GetSnapshotsAsync(string? hostname=null, int count=100, CancellationToken ct=default){
        await _semaphore.WaitAsync(ct);
        try{
            IQueryable<SystemSnapshot> query=_sysPulseDbContext.Snapshots.AsQueryable();
            if(!string.IsNullOrWhiteSpace(hostname)){
                query=query.Where(s => s.Hostname == hostname.Trim());
            }
            return await query
                .OrderByDescending(s => s.TimestampUtc)
                .Take(count)
                .ToListAsync(ct);
        }
        finally{
            _semaphore.Release();
        }
    }

    public async Task<bool> DeleteSnapshotAsync(int id, CancellationToken ct=default){
        await _semaphore.WaitAsync(ct);
        try{
            SystemSnapshot? entity=await _sysPulseDbContext.Snapshots.FindAsync([id], ct);
            if(entity is null){
                return false;
            }
            _sysPulseDbContext.Snapshots.Remove(entity);
            await _sysPulseDbContext.SaveChangesAsync(ct);
            return true;
        }
        finally{
            _semaphore.Release();
        }
    }

    public async Task<int> DeleteAllSnapshotsAsync(CancellationToken ct=default){
        await _semaphore.WaitAsync(ct);
        try{
            List<SystemSnapshot> all=await _sysPulseDbContext.Snapshots.ToListAsync(ct);
            int count=all.Count;
            _sysPulseDbContext.Snapshots.RemoveRange(all);
            await _sysPulseDbContext.SaveChangesAsync(ct);
            return count;
        }
        finally{
            _semaphore.Release();
        }
    }

    public async Task<int> GetSnapshotCountAsync(CancellationToken ct=default){
        await _semaphore.WaitAsync(ct);
        try{
            return await _sysPulseDbContext.Snapshots.CountAsync(ct);
        }
        finally{
            _semaphore.Release();
        }
    }
}

