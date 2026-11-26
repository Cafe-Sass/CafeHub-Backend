using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Attendance.DTOs;

namespace CafeHub.Application.Attendance.Contracts
{
    /// <summary>
    /// Persistence port for per-branch attendance devices and their staff code mappings. Only the device key
    /// hash is stored; creation, revocation, and mapping commit their change and one tenant audit entry
    /// together, and the hash never enters an audit snapshot.
    /// </summary>
    public interface IAttendanceDeviceStore
    {
        Task<Result<AttendanceDeviceResponse>> CreateAsync(
            Guid branchId,
            string name,
            string keyHash,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<AttendanceDeviceResponse>> ListAsync(
            Guid branchId,
            CancellationToken cancellationToken);

        Task<Result<AttendanceDeviceResponse>> RevokeAsync(
            Guid deviceId,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<AttendanceDeviceCodeResponse>> MapCodeAsync(
            Guid branchId,
            Guid staffId,
            string deviceCode,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result> UnmapCodeAsync(
            Guid branchId,
            Guid staffId,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
