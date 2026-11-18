using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class ReactivateProductCommandHandler(
        IProductStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<ReactivateProductCommand, ProductResponse>
    {
        public Task<Result<ProductResponse>> Handle(ReactivateProductCommand command, CancellationToken cancellationToken)
        {
            if (!ProductActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(Result.Failure<ProductResponse>(AuthErrors.CurrentTenantUserUnavailable()));
            }

            if (!ProductVersion.TryParse(command.ConcurrencyToken, out var version))
            {
                return Task.FromResult(Result.Failure<ProductResponse>(ValidationError.Create(
                    [new("concurrencyToken", "The concurrency token is invalid.")])));
            }

            return store.ChangeStatusAsync(command.Id, ProductStatus.Active, version, actorId, cancellationToken,
                command.IncludeHiddenCategories);
        }
    }
}
