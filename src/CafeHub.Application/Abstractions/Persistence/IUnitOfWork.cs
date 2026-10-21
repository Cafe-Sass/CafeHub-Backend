namespace CafeHub.Application.Abstractions.Persistence
{
    public interface IUnitOfWork
    {
        Task BeginTransactionAsync(CancellationToken cancellationToken);

        /// <summary>Saves pending changes and then commits the transaction opened by <see cref="BeginTransactionAsync"/>.</summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken);

        Task RollbackTransactionAsync(CancellationToken cancellationToken);
    }
}
