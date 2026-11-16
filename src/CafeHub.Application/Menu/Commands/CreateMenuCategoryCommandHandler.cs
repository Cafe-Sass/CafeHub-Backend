using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class CreateMenuCategoryCommandHandler(
        IMenuCategoryStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<CreateMenuCategoryCommand, MenuCategoryResponse>
    {
        public Task<Result<MenuCategoryResponse>> Handle(CreateMenuCategoryCommand command, CancellationToken cancellationToken)
        {
            if (!MenuCategoryActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(Result.Failure<MenuCategoryResponse>(AuthErrors.CurrentTenantUserUnavailable()));
            }

            return store.CreateAsync(command.ParentId, command.Name, command.SortOrder, actorId, cancellationToken);
        }
    }
}
