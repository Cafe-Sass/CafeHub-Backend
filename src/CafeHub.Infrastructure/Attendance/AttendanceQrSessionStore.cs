using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Audit;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Attendance
{
    internal sealed class AttendanceQrSessionStore(
        TenantDbContext context,
        IAttendanceQrTokenGenerator tokenGenerator,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IAttendanceQrSessionStore
    {
        private const string TargetType = "attendance_qr_sessions";

        public async Task<Result<AttendanceQrSessionResponse>> IssueAsync(
            Guid branchId, Guid actorUserId, CancellationToken cancellationToken)
        {
            await context.LockBranchesAsync(cancellationToken, branchId);

            var branch = await context.Branches.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == branchId, cancellationToken);
            if (branch is null || branch.LifecycleStatus != BranchLifecycleStatus.Active)
            {
                return Result.Failure<AttendanceQrSessionResponse>(AttendanceErrors.BranchNotFound());
            }

            var now = timeProvider.GetUtcNow();
            var candidates = await context.AttendanceQrSessions.AsNoTracking()
                .Where(session => session.BranchId == branchId && session.ValidTo > now)
                .ToListAsync(cancellationToken);
            var current = AttendanceQrSession.Current(candidates, now);
            if (current is not null)
            {
                return Result.Success(AttendanceQrSessionResponse.From(current));
            }

            var session = AttendanceQrSession.Create(
                Guid.CreateVersion7(), branchId, tokenGenerator.Generate(), now, actorUserId);
            context.AttendanceQrSessions.Add(session);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "attendance_qr_sessions.issued.succeeded",
                TargetType = TargetType,
                TargetId = session.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(AttendanceQrSession.BranchId), null, session.BranchId.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(AttendanceQrSession.ValidFrom),
                        null,
                        session.ValidFrom.ToString("O", CultureInfo.InvariantCulture)),
                    new TenantAuditChanges.FieldChange(
                        nameof(AttendanceQrSession.ValidTo),
                        null,
                        session.ValidTo.ToString("O", CultureInfo.InvariantCulture)),
                }),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<AttendanceQrSessionResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return Result.Success(AttendanceQrSessionResponse.From(session));
            }
            catch (DbUpdateException exception) when (IsTokenClash(exception))
            {
                return Result.Failure<AttendanceQrSessionResponse>(AttendanceErrors.QrSessionConflict());
            }
        }

        private static bool IsTokenClash(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName == "uq_attendance_qr_sessions_token";
    }
}
