using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed class GetGuestRefundRequestStatusQueryHandler(IRefundRequestStore store)
        : IQueryHandler<GetGuestRefundRequestStatusQuery, GuestRefundRequestStatus>
    {
        public async Task<Result<GuestRefundRequestStatus>> Handle(
            GetGuestRefundRequestStatusQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (!Bill.IsValidPublicReference(query.PublicReference))
            {
                return Result.Failure<GuestRefundRequestStatus>(RefundRequestErrors.NotFound());
            }

            var status = await store.FindGuestStatusAsync(query.PublicReference!, cancellationToken);
            return status is null
                ? Result.Failure<GuestRefundRequestStatus>(RefundRequestErrors.NotFound())
                : Result.Success(status);
        }
    }
}
