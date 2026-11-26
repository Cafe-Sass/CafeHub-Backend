using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Attendance
{
    internal sealed class AttendanceRecordStore(
        TenantDbContext context,
        TimeProvider timeProvider) : IAttendanceRecordStore
    {
        public async Task<Result<AttendanceScanResponse>> ScanAsync(
            Guid actorUserId,
            Guid staffBranchId,
            string token,
            CancellationToken cancellationToken)
        {
            if (actorUserId == Guid.Empty || staffBranchId == Guid.Empty)
            {
                return Result.Failure<AttendanceScanResponse>(AuthErrors.Forbidden());
            }

            if (!AttendanceQrSession.IsValidToken(token))
            {
                return Result.Failure<AttendanceScanResponse>(AttendanceErrors.InvalidToken());
            }

            var actor = await context.TenantUsers.AsNoTracking()
                .Where(user => user.Id == actorUserId)
                .Select(user => new { user.DeletedAt })
                .FirstOrDefaultAsync(cancellationToken);
            if (actor is null || actor.DeletedAt is not null)
            {
                return Result.Failure<AttendanceScanResponse>(AuthErrors.Forbidden());
            }

            var settingsMode = await context.AttendanceSettings.AsNoTracking()
                .Select(settings => settings.Mode)
                .FirstOrDefaultAsync(cancellationToken);
            var session = await context.AttendanceQrSessions.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Token == token, cancellationToken);
            var openRecord = await context.AttendanceRecords
                .FirstOrDefaultAsync(
                    record => record.StaffId == actorUserId && record.CheckOutAt == null,
                    cancellationToken);
            var sessionAlreadyUsed = session is not null
                && await context.AttendanceRecords.AsNoTracking().AnyAsync(
                    record => record.StaffId == actorUserId
                        && (record.CheckInSessionId == session.Id
                            || record.CheckOutSessionId == session.Id),
                    cancellationToken);

            var now = timeProvider.GetUtcNow();
            var decision = AttendanceScanPolicy.Decide(new AttendanceScanContext(
                settingsMode,
                session,
                actorUserId,
                true,
                staffBranchId,
                openRecord,
                sessionAlreadyUsed,
                now));
            if (decision.IsFailure)
            {
                return Result.Failure<AttendanceScanResponse>(decision.Error!);
            }

            var scan = decision.Value;
            Guid recordId;
            if (scan.CheckOutRecordId is { } recordIdToClose && openRecord is not null)
            {
                openRecord.CheckOut(scan.SessionId, now);
                recordId = recordIdToClose;
            }
            else
            {
                var record = AttendanceRecord.CheckIn(
                    Guid.CreateVersion7(),
                    scan.BranchId,
                    actorUserId,
                    scan.SessionId,
                    AttendanceSource.Qr,
                    now);
                context.AttendanceRecords.Add(record);
                recordId = record.Id;
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsAttendanceConflict(exception))
            {
                return Result.Failure<AttendanceScanResponse>(AttendanceErrors.InvalidToken());
            }

            return Result.Success(new AttendanceScanResponse(
                recordId,
                scan.IsCheckOut ? AttendanceScanResponse.CheckedOut : AttendanceScanResponse.CheckedIn,
                scan.BranchId,
                now));
        }

        private static bool IsAttendanceConflict(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName is
                "uq_attendance_records_open_staff"
                or "uq_attendance_records_staff_check_in_session"
                or "uq_attendance_records_staff_check_out_session";
    }
}
