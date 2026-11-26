using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Attendance
{
    internal static class AttendanceCommandScope
    {
        internal static async Task<Result<T>> RunAsync<T>(
            ICurrentTenantUser currentUser,
            Guid branchId,
            Func<Guid, Task<Result<T>>> action)
        {
            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<T>(scope.Error!);
            }

            if (scope.Value.HasValue && scope.Value != branchId)
            {
                return Result.Failure<T>(AttendanceErrors.BranchNotFound());
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<T>(AuthErrors.Forbidden());
            }

            return await action(actorId);
        }

        internal static async Task<Result> RunAsync(
            ICurrentTenantUser currentUser,
            Guid branchId,
            Func<Guid, Task<Result>> action)
        {
            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure(scope.Error!);
            }

            if (scope.Value.HasValue && scope.Value != branchId)
            {
                return Result.Failure(AttendanceErrors.BranchNotFound());
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure(AuthErrors.Forbidden());
            }

            return await action(actorId);
        }
    }
}
