using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Guests.Contracts
{
    /// <summary>
    /// Persistence port for guest device mutes. Reads are always constrained by the caller's branch scope
    /// (a null branch is the Owner path), and the device identifier is only ever read from a stored order.
    /// </summary>
    public interface IGuestMuteStore
    {
        Task<GuestMuteTarget?> FindOrderAsync(Guid? branchId, Guid orderId, CancellationToken cancellationToken);

        Task<GuestMuteEntry?> FindActiveAsync(
            Guid branchId,
            string deviceId,
            DateTimeOffset now,
            CancellationToken cancellationToken);

        Task<GuestMuteEntry?> FindAsync(Guid? branchId, Guid muteId, CancellationToken cancellationToken);

        Task<GuestMuteListPage> ListAsync(GuestMuteListRequest request, CancellationToken cancellationToken);

        Task<IReadOnlyList<GuestMuteSummaryEntry>> ListSummaryAsync(
            GuestMuteSummaryRequest request,
            CancellationToken cancellationToken);

        Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken);

        Task<Result> CreateAsync(GuestMute mute, CancellationToken cancellationToken);

        Task<Result> LiftAsync(
            Guid muteId,
            Guid liftedByUserId,
            DateTimeOffset now,
            CancellationToken cancellationToken);
    }
}
