using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Commands
{
    public sealed record UpdateMenuCategoryCommand(Guid Id, string Name, int SortOrder, string ConcurrencyToken)
        : ICommand<MenuCategoryResponse>, ITenantTransactionalCommand;
}
