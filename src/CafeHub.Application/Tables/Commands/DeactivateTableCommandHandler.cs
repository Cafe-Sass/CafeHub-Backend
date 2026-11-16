using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class DeactivateTableCommandHandler : ICommandHandler<DeactivateTableCommand, TableResponse>
    {
        private readonly ITableStore tableStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public DeactivateTableCommandHandler(
            ITableStore tableStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.tableStore = tableStore ?? throw new ArgumentNullException(nameof(tableStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<TableResponse>> Handle(DeactivateTableCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var tenantContext = tenantContextAccessor.GetRequired();
            if (!TableActor.TryGetActorUserId(currentTenantUser, tenantContext, out var userId))
            {
                return Result.Failure<TableResponse>(AuthErrors.CurrentTenantUserUnavailable());
            }

            var scope = TenantBranchScope.Resolve(currentTenantUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<TableResponse>(scope.Error!);
            }

            return await tableStore.DeactivateAsync(
                command.TableId,
                scope.Value,
                command.ConcurrencyToken,
                userId,
                cancellationToken);
        }
    }
}
