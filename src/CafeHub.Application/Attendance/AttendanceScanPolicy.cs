using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance
{
    public static class AttendanceScanPolicy
    {
        public static Result<AttendanceScanDecision> Decide(AttendanceScanContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.ActorUserId == Guid.Empty || !context.ActorIsActive)
            {
                return Result.Failure<AttendanceScanDecision>(AuthErrors.Forbidden());
            }

            if (context.ActorBranchId is not { } staffBranchId || staffBranchId == Guid.Empty)
            {
                return Result.Failure<AttendanceScanDecision>(AuthErrors.Forbidden());
            }

            if (context.Mode != AttendanceMode.Qr
                || context.Session is not { } session
                || !session.IsValid(context.Now)
                || session.BranchId != staffBranchId
                || context.SessionAlreadyUsed)
            {
                return Result.Failure<AttendanceScanDecision>(AttendanceErrors.InvalidToken());
            }

            if (context.OpenRecord is not { } openRecord)
            {
                return Result.Success(
                    new AttendanceScanDecision(session.BranchId, session.Id, null));
            }

            return openRecord.CanCheckOutWith(session.Id)
                ? Result.Success(new AttendanceScanDecision(openRecord.BranchId, session.Id, openRecord.Id))
                : Result.Failure<AttendanceScanDecision>(AttendanceErrors.InvalidToken());
        }
    }
}
