using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Persistence;

namespace CafeHub.Application
{
    internal sealed class MissingMasterUnitOfWork : IMasterUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The master unit of work is not registered. Register AddInfrastructure before dispatching master-transactional commands.");
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The master unit of work is not registered. Register AddInfrastructure before dispatching master-transactional commands.");
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The master unit of work is not registered. Register AddInfrastructure before dispatching master-transactional commands.");
        }
    }

    internal sealed class MissingTenantUnitOfWork : ITenantUnitOfWork
    {
        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The tenant unit of work is not registered. Register AddInfrastructure before dispatching tenant-transactional commands.");
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The tenant unit of work is not registered. Register AddInfrastructure before dispatching tenant-transactional commands.");
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The tenant unit of work is not registered. Register AddInfrastructure before dispatching tenant-transactional commands.");
        }
    }
}
