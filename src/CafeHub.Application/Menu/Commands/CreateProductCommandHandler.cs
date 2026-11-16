using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class CreateProductCommandHandler(
        IProductStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<CreateProductCommand, ProductResponse>
    {
        public Task<Result<ProductResponse>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
        {
            if (!ProductActor.TryGetId(currentUser, tenantContext, out var actorId))
            {
                return Task.FromResult(Result.Failure<ProductResponse>(AuthErrors.CurrentTenantUserUnavailable()));
            }

            return store.CreateAsync(command.ToWriteModel(), actorId, cancellationToken);
        }
    }
}
