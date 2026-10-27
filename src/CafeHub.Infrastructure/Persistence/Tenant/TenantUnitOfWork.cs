using CafeHub.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Tenant
{
    internal sealed class TenantUnitOfWork(TenantDbContext context) : ITenantUnitOfWork
    {
        public async Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            await context.Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            var transaction = context.Database.CurrentTransaction ?? throw new InvalidOperationException("A transaction must be started before it is committed.");
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            var transaction = context.Database.CurrentTransaction ?? throw new InvalidOperationException("A transaction must be started before it is rolled back.");
            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }
    }
}
