using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.Contracts;
using CafeHub.Application.Printing;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Attendance
{
    /// <summary>
    /// Authenticates a presented attendance device key. A malformed, unknown, revoked, or mismatched key all
    /// return the same <c>Attendance.DeviceUnauthorized</c> result, so a caller cannot tell the cases apart.
    /// </summary>
    public sealed class AttendanceDeviceAuthenticator(IAttendanceDeviceStore store)
    {
        public async Task<Result<AttendanceDeviceAccess>> AuthenticateAsync(
            string? presentedKey,
            CancellationToken cancellationToken)
        {
            if (!ConnectorKeys.IsWellFormed(presentedKey))
            {
                return Result.Failure<AttendanceDeviceAccess>(AttendanceErrors.DeviceUnauthorized());
            }

            var presentedHash = ConnectorKeys.Hash(presentedKey!);
            var device = await store.FindByKeyHashAsync(presentedHash, cancellationToken);
            if (device is null
                || !ConnectorKeys.Matches(device.KeyHash, presentedHash)
                || device.Status != AttendanceDeviceStatus.Active)
            {
                return Result.Failure<AttendanceDeviceAccess>(AttendanceErrors.DeviceUnauthorized());
            }

            return Result.Success(device);
        }
    }
}
