using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Queries
{
    public sealed class GetStaffByIdQueryHandler : IQueryHandler<GetStaffByIdQuery, StaffResponse>
    {
        private readonly IStaffStore staffStore;

        public GetStaffByIdQueryHandler(IStaffStore staffStore)
        {
            this.staffStore = staffStore ?? throw new ArgumentNullException(nameof(staffStore));
        }

        public async Task<Result<StaffResponse>> Handle(GetStaffByIdQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var staff = await staffStore.GetByIdAsync(query.StaffId, cancellationToken);
            if (staff is null)
            {
                return Result.Failure<StaffResponse>(StaffErrors.NotFound());
            }

            return Result.Success(staff);
        }
    }
}
