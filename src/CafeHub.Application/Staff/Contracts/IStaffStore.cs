using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Contracts
{
    public interface IStaffStore
    {
        Task<Result<StaffResponse>> CreateAsync(
            string fullName,
            string email,
            string? phoneNumber,
            Guid branchId,
            string rawPassword,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<StaffResponse>> UpdateAsync(
            Guid staffId,
            string fullName,
            string? phoneNumber,
            Guid branchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<StaffResponse?> GetByIdAsync(Guid staffId, CancellationToken cancellationToken);

        Task<StaffListPage> ListAsync(StaffListRequest request, CancellationToken cancellationToken);

        Task<Result> DeactivateAsync(Guid staffId, Guid actorUserId, CancellationToken cancellationToken);

        Task<Result> ReactivateAsync(Guid staffId, Guid actorUserId, CancellationToken cancellationToken);

        Task<Result> ResetPasswordAsync(Guid staffId, string newPassword, Guid actorUserId, CancellationToken cancellationToken);

        Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken);
    }
}
