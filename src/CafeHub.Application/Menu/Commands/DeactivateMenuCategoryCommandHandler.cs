using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class DeactivateMenuCategoryCommandHandler(
        IMenuCategoryStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<DeactivateMenuCategoryCommand, MenuCategoryResponse>
    {
        public Task<Result<MenuCategoryResponse>> Handle(DeactivateMenuCategoryCommand command, CancellationToken cancellationToken)
        {
            if (!MenuCategoryActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(Result.Failure<MenuCategoryResponse>(AuthErrors.CurrentTenantUserUnavailable()));
            }

            if (!MenuCategoryVersion.TryParse(command.ConcurrencyToken, out var version))
            {
                return Task.FromResult(Result.Failure<MenuCategoryResponse>(ValidationError.Create(
                    [new("concurrencyToken", "The concurrency token is invalid.")])));
            }

            return store.ChangeStatusAsync(command.Id, MenuCategoryStatus.Inactive, version, actorId, cancellationToken);
        }
    }
}
