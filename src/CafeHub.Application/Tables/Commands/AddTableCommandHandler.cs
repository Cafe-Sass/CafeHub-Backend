using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class AddTableCommandHandler : ICommandHandler<AddTableCommand, TableResponse>
    {
        private readonly ITableStore tableStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public AddTableCommandHandler(
            ITableStore tableStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.tableStore = tableStore ?? throw new ArgumentNullException(nameof(tableStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<TableResponse>> Handle(AddTableCommand command, CancellationToken cancellationToken)
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

            if (scope.Value is { } branchId && branchId != command.BranchId)
            {
                return Result.Failure<TableResponse>(TablesErrors.NotFound());
            }

            var label = command.Label?.Trim();
            return await tableStore.AddAsync(command.BranchId, label, userId, cancellationToken);
        }
    }
}
