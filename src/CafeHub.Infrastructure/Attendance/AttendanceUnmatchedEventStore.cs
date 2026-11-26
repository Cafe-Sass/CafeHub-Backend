using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Application.Audit;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Attendance
{
    internal sealed class AttendanceUnmatchedEventStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IAttendanceUnmatchedEventStore
    {
        private const string TargetType = "attendance_device_events";

        public async Task<IReadOnlyList<AttendanceUnmatchedEventResponse>> ListAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var rows = await (
                    from deviceEvent in context.AttendanceDeviceEvents.AsNoTracking()
                    join device in context.AttendanceDevices.AsNoTracking()
                        on deviceEvent.DeviceId equals device.Id
                    where device.BranchId == branchId
                        && deviceEvent.Status == AttendanceDeviceEventStatus.Unmatched
                    orderby deviceEvent.OccurredAt, deviceEvent.Id
                    select new
                    {
                        deviceEvent.Id,
                        deviceEvent.DeviceId,
                        DeviceName = device.Name,
                        deviceEvent.DeviceCode,
                        deviceEvent.OccurredAt,
                        deviceEvent.Direction,
                        deviceEvent.ReceivedAt,
                    })
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new AttendanceUnmatchedEventResponse(
                    row.Id,
                    row.DeviceId,
                    row.DeviceName,
                    row.DeviceCode,
                    row.OccurredAt,
                    row.Direction?.ToString(),
                    row.ReceivedAt))
                .ToArray();
        }

        public async Task<Result<AttendanceDeviceReprocessResponse>> ReprocessAsync(
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var events = await (
                    from deviceEvent in context.AttendanceDeviceEvents
                    join device in context.AttendanceDevices
                        on deviceEvent.DeviceId equals device.Id
                    where device.BranchId == branchId
                        && deviceEvent.Status == AttendanceDeviceEventStatus.Unmatched
                    orderby deviceEvent.OccurredAt, deviceEvent.Id
                    select deviceEvent)
                .ToListAsync(cancellationToken);
            if (events.Count == 0)
            {
                return Result.Success(new AttendanceDeviceReprocessResponse(0, 0, 0));
            }

            var codes = events
                .Select(deviceEvent => deviceEvent.DeviceCode)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var mappings = await context.AttendanceDeviceCodes.AsNoTracking()
                .Where(code => code.BranchId == branchId && codes.Contains(code.DeviceCode))
                .ToDictionaryAsync(
                    code => code.DeviceCode,
                    code => code.StaffId,
                    StringComparer.Ordinal,
                    cancellationToken);
            var staffIds = mappings.Values.Distinct().ToArray();
            var staffRows = await context.TenantUsers.AsNoTracking()
                .Where(user => staffIds.Contains(user.Id))
                .Select(user => new { user.Id, user.BranchId, user.DeletedAt })
                .ToListAsync(cancellationToken);
            var staff = staffRows.ToDictionary(row => row.Id);
            var openRows = await context.AttendanceRecords
                .Where(record => staffIds.Contains(record.StaffId) && record.CheckOutAt == null)
                .ToListAsync(cancellationToken);
            var openRecords = openRows.ToDictionary(
                record => record.StaffId,
                record => (AttendanceRecord?)record);

            var now = timeProvider.GetUtcNow();
            var processed = 0;
            var unmatched = 0;
            var rejected = 0;

            foreach (var deviceEvent in events)
            {
                if (!mappings.TryGetValue(deviceEvent.DeviceCode, out var staffId)
                    || !staff.TryGetValue(staffId, out var user))
                {
                    unmatched++;
                    continue;
                }

                openRecords.TryGetValue(staffId, out var openRecord);
                var decision = AttendanceDeviceEventPolicy.Decide(new AttendanceDeviceEventContext(
                    branchId,
                    staffId,
                    user.BranchId,
                    user.DeletedAt is null,
                    openRecord,
                    deviceEvent.OccurredAt));

                switch (decision.Pairing)
                {
                    case AttendanceDeviceEventPairing.Unmatched:
                        unmatched++;
                        break;
                    case AttendanceDeviceEventPairing.Rejected:
                        rejected++;
                        break;
                    case AttendanceDeviceEventPairing.CheckIn:
                        var record = AttendanceRecord.CheckInFromDevice(
                            Guid.CreateVersion7(),
                            branchId,
                            staffId,
                            deviceEvent.OccurredAt,
                            now);
                        context.AttendanceRecords.Add(record);
                        openRecords[staffId] = record;
                        deviceEvent.MarkProcessed(record.Id, now);
                        processed++;
                        break;
                    default:
                        var open = openRecord!;
                        open.CheckOutFromDevice(deviceEvent.OccurredAt, now);
                        openRecords[staffId] = null;
                        deviceEvent.MarkProcessed(open.Id, now);
                        processed++;
                        break;
                }
            }

            if (processed == 0 && rejected == 0)
            {
                return Result.Success(new AttendanceDeviceReprocessResponse(0, unmatched, rejected));
            }

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "attendance_device_events.reprocessed.succeeded",
                TargetType = TargetType,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        "Processed", null, processed.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new TenantAuditChanges.FieldChange(
                        "Unmatched", null, unmatched.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new TenantAuditChanges.FieldChange(
                        "Rejected", null, rejected.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new TenantAuditChanges.FieldChange("BranchId", null, branchId.ToString()),
                }),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<AttendanceDeviceReprocessResponse>(audit.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                return Result.Failure<AttendanceDeviceReprocessResponse>(
                    AttendanceErrors.DeviceEventConflict());
            }

            return Result.Success(new AttendanceDeviceReprocessResponse(processed, unmatched, rejected));
        }

        private static bool IsUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
