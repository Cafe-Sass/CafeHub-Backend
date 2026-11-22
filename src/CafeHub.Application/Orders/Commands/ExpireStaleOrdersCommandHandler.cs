using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders.Contracts;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class ExpireStaleOrdersCommandHandler(
        IOrderExpiryStore expiryStore,
        TimeProvider timeProvider)
        : ICommandHandler<ExpireStaleOrdersCommand, IReadOnlyList<Guid>>
    {
        public async Task<Result<IReadOnlyList<Guid>>> Handle(
            ExpireStaleOrdersCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var expiredIds = await expiryStore.ExpireStaleOrdersAsync(
                command.BranchId,
                command.OrderId,
                timeProvider.GetUtcNow(),
                cancellationToken);
            return Result.Success(expiredIds);
        }
    }
}
