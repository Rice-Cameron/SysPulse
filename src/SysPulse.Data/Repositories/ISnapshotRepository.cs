using SysPulse.Data.Entities;

namespace SysPulse.Data.Repositories;

public interface ISnapshotRepository
{
    Task EnsureDatabaseCreatedAsync(CancellationToken ct = default);
    Task SaveSnapshotAsync(SystemSnapshot snapshot, CancellationToken ct = default);
    Task<IReadOnlyList<SystemSnapshot>> GetRecentSnapshotsAsync(int count = 60, CancellationToken ct = default);
}
