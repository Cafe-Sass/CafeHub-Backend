using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Tables
{
    internal static class TableActor
    {
        internal static Result EnsureBranchScope(ICurrentTenantUser currentTenantUser, Guid branchId)
        {
            var scope = TenantBranchScope.Resolve(currentTenantUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure(scope.Error!);
            }

            return scope.Value is { } assignedBranchId && assignedBranchId != branchId
                ? Result.Failure(TablesErrors.NotFound())
                : Result.Success();
        }

        internal static bool TryGetActorUserId(
            ICurrentTenantUser currentTenantUser,
            TenantContext tenantContext,
            out Guid userId)
        {
            var candidate = currentTenantUser.UserId;
            userId = candidate ?? Guid.Empty;
            return currentTenantUser.IsAuthenticated
                && candidate is { } value
                && value != Guid.Empty
                && currentTenantUser.TenantId is not null
                && currentTenantUser.TenantId == tenantContext.TenantId
                && tenantContext.IsActive;
        }
    }
}
