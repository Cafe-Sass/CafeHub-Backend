using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Commands
{
    public sealed record DeactivateProductCommand(Guid Id, string ConcurrencyToken)
        : ICommand<ProductResponse>, ITenantTransactionalCommand;
}
