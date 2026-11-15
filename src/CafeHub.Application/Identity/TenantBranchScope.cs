using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public static class TenantBranchScope
    {
        public static Result<Guid?> Resolve(TenantAccountAccess? account)
        {
            if (account is { IsStaff: false })
            {
                return Result.Success<Guid?>(null);
            }

            return account is { HasActiveBranch: true, BranchId: { } branchId }
                ? Result.Success<Guid?>(branchId)
                : Result.Failure<Guid?>(AuthErrors.Forbidden());
        }
    }
}
