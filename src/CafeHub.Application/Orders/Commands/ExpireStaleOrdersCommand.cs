using CafeHub.Application.Abstractions.Messaging;

namespace CafeHub.Application.Orders.Commands
{
    public sealed record ExpireStaleOrdersCommand(Guid? BranchId, Guid? OrderId) : ICommand<IReadOnlyList<Guid>>;
}
