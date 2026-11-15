using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Queries
{
    public sealed class GetStaffByIdQueryHandler : IQueryHandler<GetStaffByIdQuery, StaffResponse>
    {
        private readonly IStaffStore staffStore;
        private readonly ICurrentTenantUser currentUser;

        public GetStaffByIdQueryHandler(IStaffStore staffStore, ICurrentTenantUser currentUser)
        {
            this.staffStore = staffStore ?? throw new ArgumentNullException(nameof(staffStore));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<Result<StaffResponse>> Handle(GetStaffByIdQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<StaffResponse>(scope.Error!);
            }

            var staff = await staffStore.GetByIdAsync(query.StaffId, cancellationToken, scope.Value);
            if (staff is null)
            {
                return Result.Failure<StaffResponse>(StaffErrors.NotFound());
            }

            return Result.Success(staff);
        }
    }
}
