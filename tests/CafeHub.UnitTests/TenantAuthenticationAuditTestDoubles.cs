using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;

namespace CafeHub.UnitTests
{
    internal sealed class RecordingTenantAuditWriter : ITenantAuditWriter
    {
        public List<TenantAuditEntry> Entries { get; } = [];

        public List<TenantAuditEntry> PersistedEntries { get; } = [];

        public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.FromResult(Result.Success());
        }

        public void CommitPendingEntries()
        {
            PersistedEntries.AddRange(Entries.Skip(PersistedEntries.Count));
        }
    }

    internal sealed class RecordingTenantAuditUnitOfWork(RecordingTenantAuditWriter? auditWriter = null) : ITenantUnitOfWork
    {
        public int BeginCount { get; private set; }

        public int CommitCount { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            auditWriter?.CommitPendingEntries();
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
