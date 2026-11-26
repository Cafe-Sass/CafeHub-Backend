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
    internal sealed class AttendanceDeviceStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IAttendanceDeviceStore
    {
        private const string DeviceTargetType = "attendance_devices";
        private const string CodeTargetType = "attendance_device_codes";

        public async Task<AttendanceDeviceAccess?> FindByKeyHashAsync(
            string keyHash,
            CancellationToken cancellationToken) =>
            await context.AttendanceDevices.AsNoTracking()
                .Where(device => device.KeyHash == keyHash)
                .Select(device => new AttendanceDeviceAccess(
                    device.Id,
                    device.BranchId,
                    device.Name,
                    device.KeyHash,
                    device.Status))
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<Result<AttendanceDeviceResponse>> CreateAsync(
            Guid branchId,
            string name,
            string keyHash,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var branchExists = await context.Branches.AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId, cancellationToken);
            if (!branchExists)
            {
                return Result.Failure<AttendanceDeviceResponse>(AttendanceErrors.BranchNotFound());
            }

            var now = timeProvider.GetUtcNow();
            var device = AttendanceDevice.Create(
                Guid.CreateVersion7(),
                branchId,
                name,
                keyHash,
                now,
                actorUserId);
            context.AttendanceDevices.Add(device);

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "attendance_devices.created.succeeded",
                TargetType = DeviceTargetType,
                TargetId = device.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange(nameof(AttendanceDevice.Name), null, device.Name),
                    new TenantAuditChanges.FieldChange(
                        nameof(AttendanceDevice.BranchId), null, device.BranchId.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(AttendanceDevice.Status), null, device.Status.ToString()),
                }),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<AttendanceDeviceResponse>(audit.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(AttendanceDeviceResponse.From(device));
        }

        public async Task<IReadOnlyList<AttendanceDeviceResponse>> ListAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var devices = await context.AttendanceDevices.AsNoTracking()
                .Where(device => device.BranchId == branchId)
                .OrderBy(device => device.Name)
                .ThenBy(device => device.Id)
                .ToListAsync(cancellationToken);

            return devices.Select(AttendanceDeviceResponse.From).ToArray();
        }

        public async Task<Result<AttendanceDeviceResponse>> RevokeAsync(
            Guid deviceId,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var device = await context.AttendanceDevices.FirstOrDefaultAsync(
                candidate => candidate.Id == deviceId && candidate.BranchId == branchId,
                cancellationToken);
            if (device is null)
            {
                return Result.Failure<AttendanceDeviceResponse>(AttendanceErrors.DeviceNotFound());
            }

            if (!device.Revoke(timeProvider.GetUtcNow(), actorUserId))
            {
                return Result.Success(AttendanceDeviceResponse.From(device));
            }

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "attendance_devices.revoked.succeeded",
                TargetType = DeviceTargetType,
                TargetId = device.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(AttendanceDevice.Status),
                        AttendanceDeviceStatus.Active.ToString(),
                        device.Status.ToString()),
                }),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<AttendanceDeviceResponse>(audit.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(AttendanceDeviceResponse.From(device));
        }

        public async Task<Result<AttendanceDeviceCodeResponse>> MapCodeAsync(
            Guid branchId,
            Guid staffId,
            string deviceCode,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var staffExists = await context.TenantUsers.AsNoTracking()
                .AnyAsync(
                    user => user.Id == staffId
                        && user.BranchId == branchId
                        && user.DeletedAt == null,
                    cancellationToken);
            if (!staffExists)
            {
                return Result.Failure<AttendanceDeviceCodeResponse>(AttendanceErrors.StaffNotFound());
            }

            var now = timeProvider.GetUtcNow();
            var code = await context.AttendanceDeviceCodes.FirstOrDefaultAsync(
                candidate => candidate.BranchId == branchId && candidate.StaffId == staffId,
                cancellationToken);

            TenantAuditEntry auditEntry;
            if (code is null)
            {
                code = AttendanceDeviceCode.Create(
                    Guid.CreateVersion7(),
                    branchId,
                    staffId,
                    deviceCode,
                    now,
                    actorUserId);
                context.AttendanceDeviceCodes.Add(code);
                auditEntry = new TenantAuditEntry
                {
                    Action = "attendance_device_codes.mapped.succeeded",
                    TargetType = CodeTargetType,
                    TargetId = code.Id,
                    ActorUserId = actorUserId,
                    Changes = TenantAuditChanges.Create(new[]
                    {
                        new TenantAuditChanges.FieldChange(
                            nameof(AttendanceDeviceCode.BranchId), null, code.BranchId.ToString()),
                        new TenantAuditChanges.FieldChange(
                            nameof(AttendanceDeviceCode.StaffId), null, code.StaffId.ToString()),
                        new TenantAuditChanges.FieldChange(
                            nameof(AttendanceDeviceCode.DeviceCode), null, code.DeviceCode),
                    }),
                };
            }
            else
            {
                var previousCode = code.DeviceCode;
                if (!code.ChangeCode(deviceCode, now, actorUserId))
                {
                    return Result.Success(AttendanceDeviceCodeResponse.From(code));
                }

                auditEntry = new TenantAuditEntry
                {
                    Action = "attendance_device_codes.mapped.succeeded",
                    TargetType = CodeTargetType,
                    TargetId = code.Id,
                    ActorUserId = actorUserId,
                    Changes = TenantAuditChanges.Update(new[]
                    {
                        new TenantAuditChanges.FieldChange(
                            nameof(AttendanceDeviceCode.DeviceCode), previousCode, code.DeviceCode),
                    }),
                };
            }

            var audit = await auditWriter.WriteAsync(auditEntry, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<AttendanceDeviceCodeResponse>(audit.Error!);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsCodeConflict(exception))
            {
                return Result.Failure<AttendanceDeviceCodeResponse>(AttendanceErrors.DeviceCodeConflict());
            }

            return Result.Success(AttendanceDeviceCodeResponse.From(code));
        }

        public async Task<Result> UnmapCodeAsync(
            Guid branchId,
            Guid staffId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var code = await context.AttendanceDeviceCodes.FirstOrDefaultAsync(
                candidate => candidate.BranchId == branchId && candidate.StaffId == staffId,
                cancellationToken);
            if (code is null)
            {
                return Result.Failure(AttendanceErrors.DeviceCodeNotFound());
            }

            context.AttendanceDeviceCodes.Remove(code);
            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "attendance_device_codes.unmapped.succeeded",
                TargetType = CodeTargetType,
                TargetId = code.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(AttendanceDeviceCode.DeviceCode), code.DeviceCode, null),
                }),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure(audit.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private static bool IsCodeConflict(DbUpdateException exception) =>
            exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName is
                "uq_attendance_device_codes_branch_code"
                or "uq_attendance_device_codes_branch_staff";
    }
}
