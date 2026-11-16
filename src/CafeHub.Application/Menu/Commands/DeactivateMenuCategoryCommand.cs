using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Commands
{
    public sealed record DeactivateMenuCategoryCommand(Guid Id, string ConcurrencyToken)
        : ICommand<MenuCategoryResponse>, ITenantTransactionalCommand;
}
