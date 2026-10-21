using CafeHub.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Persistence.Master
{
    internal sealed class MasterUnitOfWork(MasterDbContext context) : IMasterUnitOfWork
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
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            var transaction = context.Database.CurrentTransaction ?? throw new InvalidOperationException("A transaction must be started before it is rolled back.");
            await transaction.RollbackAsync(cancellationToken);
        }
    }
}
