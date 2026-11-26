using System.Globalization;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Audit;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Attendance
{
    internal sealed class AttendanceSettingsStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IAttendanceSettingsReader, IAttendanceSettingsWriter
    {
        public async Task<AttendanceSettingsResponse?> GetAsync(CancellationToken cancellationToken)
        {
            var settings = await context.AttendanceSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return null;
            }

            return AttendanceSettingsResponse.From(settings, CurrentToken(settings));
        }

        public async Task<Result<AttendanceSettingsResponse>> UpdateAsync(
            AttendanceMode mode,
            Guid actorUserId,
            string concurrencyToken,
            CancellationToken cancellationToken)
        {
            var settings = await context.AttendanceSettings.FirstOrDefaultAsync(cancellationToken);
            if (settings is null)
            {
                return Result.Failure<AttendanceSettingsResponse>(AttendanceErrors.SettingsNotFound());
            }

            if (!uint.TryParse(
                    concurrencyToken,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var token))
            {
                return Result.Failure<AttendanceSettingsResponse>(ValidationError.Create(new[]
                {
                    new FieldError(
                        "concurrencyToken", "The concurrency token is not a valid unsigned integer."),
                }));
            }

            if (!AttendanceSettings.IsValidMode(mode))
            {
                return Result.Failure<AttendanceSettingsResponse>(AttendanceErrors.InvalidMode());
            }

            if (settings.Mode == mode)
            {
                return Result.Success(AttendanceSettingsResponse.From(settings, CurrentToken(settings)));
            }

            context.Entry(settings).Property<uint>("xmin").OriginalValue = token;
            var changes = TenantAuditChangeSets.AttendanceSettingsUpdate(settings, mode);

            settings.Update(mode, timeProvider.GetUtcNow(), actorUserId);

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "attendance_settings.update.succeeded",
                TargetType = "attendance_settings",
                TargetId = settings.Id,
                ActorUserId = actorUserId,
                Changes = changes,
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<AttendanceSettingsResponse>(auditResult.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure<AttendanceSettingsResponse>(
                    AttendanceErrors.ConcurrencyConflict());
            }

            return Result.Success(AttendanceSettingsResponse.From(settings, CurrentToken(settings)));
        }

        private string CurrentToken(AttendanceSettings settings) =>
            context.Entry(settings)
                .Property<uint>("xmin")
                .CurrentValue
                .ToString(CultureInfo.InvariantCulture);
    }
}
