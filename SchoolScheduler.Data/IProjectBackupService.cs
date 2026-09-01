namespace SchoolScheduler.Data;

public interface IProjectBackupService
{
    Task CreateAsync(string backupPath, CancellationToken cancellationToken = default);
    Task RestoreAsync(string backupPath, CancellationToken cancellationToken = default);
}
