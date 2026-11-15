using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tables.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Tables.Commands
{
    public sealed class AddTablesBulkCommandHandler : ICommandHandler<AddTablesBulkCommand, TableBulkResult>
    {
        private readonly ITableStore tableStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public AddTablesBulkCommandHandler(
            ITableStore tableStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.tableStore = tableStore ?? throw new ArgumentNullException(nameof(tableStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<TableBulkResult>> Handle(AddTablesBulkCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var tenantContext = tenantContextAccessor.GetRequired();
            if (!TableActor.TryGetActorUserId(currentTenantUser, tenantContext, out var userId))
            {
                return Result.Failure<TableBulkResult>(AuthErrors.CurrentTenantUserUnavailable());
            }

            var scope = TenantBranchScope.Resolve(currentTenantUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<TableBulkResult>(scope.Error!);
            }

            if (scope.Value is { } branchId && branchId != command.BranchId)
            {
                return Result.Failure<TableBulkResult>(TablesErrors.NotFound());
            }

            return await tableStore.AddBulkAsync(command.BranchId, command.Count, userId, cancellationToken);
        }
    }
}
