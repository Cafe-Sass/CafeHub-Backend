using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class UpdateProductCommandHandler(
        IProductStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<UpdateProductCommand, ProductResponse>
    {
        public Task<Result<ProductResponse>> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
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

            return store.UpdateAsync(command.Id, command.ToWriteModel(), version, actorId, cancellationToken);
        }
    }
}
