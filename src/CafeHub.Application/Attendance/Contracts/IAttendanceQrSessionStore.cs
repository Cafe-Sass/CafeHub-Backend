using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Contracts
{
    public interface IAttendanceQrSessionStore
    {
        Task<Result<AttendanceQrSessionResponse>> IssueAsync(
            Guid branchId, Guid actorUserId, CancellationToken cancellationToken);
    }
}
