using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Attendance.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Attendance
{
    internal sealed class AttendanceDeviceEventStore(TenantDbContext context)
        : IAttendanceDeviceEventStore
    {
        public async Task<Result<AttendanceDeviceEventsResponse>> IngestAsync(
            Guid deviceId,
            Guid branchId,
            IReadOnlyList<AttendanceDeviceEventInput> events,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var mode = await context.AttendanceSettings.AsNoTracking()
                .Select(settings => settings.Mode)
                .FirstOrDefaultAsync(cancellationToken);
            if (mode != AttendanceMode.Device)
            {
                return Result.Failure<AttendanceDeviceEventsResponse>(
                    AttendanceErrors.DeviceModeNotEnabled());
            }

            var device = await context.AttendanceDevices.FirstOrDefaultAsync(
                candidate => candidate.Id == deviceId
                    && candidate.BranchId == branchId
                    && candidate.Status == AttendanceDeviceStatus.Active,
                cancellationToken);
            if (device is null)
            {
                return Result.Failure<AttendanceDeviceEventsResponse>(
                    AttendanceErrors.DeviceUnauthorized());
            }

            var inputs = events.ToArray();
            var eventIds = inputs
                .Select(item => item.EventId!.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var existingIds = await context.AttendanceDeviceEvents.AsNoTracking()
                .Where(deviceEvent => deviceEvent.DeviceId == deviceId
                    && eventIds.Contains(deviceEvent.DeviceEventId))
                .Select(deviceEvent => deviceEvent.DeviceEventId)
                .ToListAsync(cancellationToken);
            var existing = existingIds.ToHashSet(StringComparer.Ordinal);

            var codes = inputs
                .Select(item => item.Code!.Trim())
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

            var outcomes = new AttendanceDeviceEventOutcome[inputs.Length];
            var classified = AttendanceDeviceEventBatch.Classify(inputs, existing, now);
            foreach (var item in classified)
            {
                var eventId = item.Input.EventId!.Trim();
                if (item.IsDuplicate)
                {
                    outcomes[item.Index] = new AttendanceDeviceEventOutcome(
                        eventId, AttendanceDeviceEventOutcomes.Duplicate, null, null);
                }
                else if (item.IsOutsideWindow)
                {
                    outcomes[item.Index] = new AttendanceDeviceEventOutcome(
                        eventId,
                        AttendanceDeviceEventOutcomes.Rejected,
                        null,
                        AttendanceDeviceEventReasons.OutsideWindow);
                }
            }

            var ordered = classified
                .Where(item => !item.IsDuplicate && !item.IsOutsideWindow)
                .OrderBy(item => item.Input.OccurredAt)
                .ThenBy(item => item.Index);

            foreach (var item in ordered)
            {
                var input = item.Input;
                var eventId = input.EventId!.Trim();
                var code = input.Code!.Trim();
                var deviceEvent = AttendanceDeviceEvent.Create(
                    Guid.CreateVersion7(),
                    deviceId,
                    eventId,
                    code,
                    input.OccurredAt,
                    ParseDirection(input.Direction),
                    now);

                if (!mappings.TryGetValue(code, out var staffId)
                    || !staff.TryGetValue(staffId, out var user))
                {
                    context.AttendanceDeviceEvents.Add(deviceEvent);
                    outcomes[item.Index] = new AttendanceDeviceEventOutcome(
                        eventId, AttendanceDeviceEventOutcomes.Unmatched, null, null);
                    continue;
                }

                openRecords.TryGetValue(staffId, out var openRecord);
                var decision = AttendanceDeviceEventPolicy.Decide(new AttendanceDeviceEventContext(
                    branchId,
                    staffId,
                    user.BranchId,
                    user.DeletedAt is null,
                    openRecord,
                    input.OccurredAt));

                switch (decision.Pairing)
                {
                    case AttendanceDeviceEventPairing.Unmatched:
                        context.AttendanceDeviceEvents.Add(deviceEvent);
                        outcomes[item.Index] = new AttendanceDeviceEventOutcome(
                            eventId, AttendanceDeviceEventOutcomes.Unmatched, null, null);
                        break;
                    case AttendanceDeviceEventPairing.Rejected:
                        outcomes[item.Index] = new AttendanceDeviceEventOutcome(
                            eventId,
                            AttendanceDeviceEventOutcomes.Rejected,
                            null,
                            AttendanceDeviceEventReasons.OutOfOrder);
                        break;
                    case AttendanceDeviceEventPairing.CheckIn:
                        var record = AttendanceRecord.CheckInFromDevice(
                            Guid.CreateVersion7(),
                            branchId,
                            staffId,
                            input.OccurredAt,
                            now);
                        context.AttendanceRecords.Add(record);
                        openRecords[staffId] = record;
                        deviceEvent.MarkProcessed(record.Id, now);
                        context.AttendanceDeviceEvents.Add(deviceEvent);
                        outcomes[item.Index] = new AttendanceDeviceEventOutcome(
                            eventId, AttendanceDeviceEventOutcomes.Processed, record.Id, null);
                        break;
                    default:
                        var open = openRecord!;
                        open.CheckOutFromDevice(input.OccurredAt, now);
                        openRecords[staffId] = null;
                        deviceEvent.MarkProcessed(open.Id, now);
                        context.AttendanceDeviceEvents.Add(deviceEvent);
                        outcomes[item.Index] = new AttendanceDeviceEventOutcome(
                            eventId, AttendanceDeviceEventOutcomes.Processed, open.Id, null);
                        break;
                }
            }

            device.MarkSeen(now);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                return Result.Failure<AttendanceDeviceEventsResponse>(
                    AttendanceErrors.DeviceEventConflict());
            }

            return Result.Success(new AttendanceDeviceEventsResponse(outcomes));
        }

        private static AttendanceDeviceEventDirection? ParseDirection(string? value) =>
            value is not null
                && Enum.TryParse<AttendanceDeviceEventDirection>(value, ignoreCase: true, out var direction)
                && Enum.IsDefined(direction)
                ? direction
                : null;

        private static bool IsUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
