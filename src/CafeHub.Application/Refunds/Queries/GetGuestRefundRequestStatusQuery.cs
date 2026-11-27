using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Refunds.DTOs;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed record GetGuestRefundRequestStatusQuery(string? PublicReference) : IQuery<GuestRefundRequestStatus>;
}
