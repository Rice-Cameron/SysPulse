using SysPulse.Data.Entities;

namespace SysPulse.Data.Repositories;

public interface ISnapshotRepository{
    Task EnsureDatabaseCreatedAsync(CancellationToken ct=default);
    Task SaveSnapshotAsync(SystemSnapshot snapshot, CancellationToken ct=default);
    Task<IReadOnlyList<SystemSnapshot>> GetRecentSnapshotsAsync(int count=60, CancellationToken ct=default);
    Task<IReadOnlyList<SystemSnapshot>> GetSnapshotsAsync(string? hostname=null, int count=100, CancellationToken ct=default);
    Task<bool> DeleteSnapshotAsync(int id, CancellationToken ct=default);
    Task<int> DeleteAllSnapshotsAsync(CancellationToken ct=default);
}
