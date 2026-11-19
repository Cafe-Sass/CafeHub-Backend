using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Commands
{
    public sealed record ReactivateProductCommand(Guid Id, string ConcurrencyToken, bool IncludeHiddenCategories = false)
        : ICommand<ProductResponse>, ITenantTransactionalCommand;
}
