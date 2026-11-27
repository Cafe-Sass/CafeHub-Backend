using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed class GetGuestBillPaymentsQueryHandler(IRefundRequestStore store)
        : IQueryHandler<GetGuestBillPaymentsQuery, IReadOnlyList<GuestRefundablePayment>>
    {
        public async Task<Result<IReadOnlyList<GuestRefundablePayment>>> Handle(
            GetGuestBillPaymentsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (!Bill.IsValidPublicReference(query.BillReference))
            {
                return Result.Failure<IReadOnlyList<GuestRefundablePayment>>(RefundRequestErrors.NotFound());
            }

            var payments = await store.ListConfirmedPaymentsForBillAsync(query.BillReference!, cancellationToken);
            return payments is null
                ? Result.Failure<IReadOnlyList<GuestRefundablePayment>>(RefundRequestErrors.NotFound())
                : Result.Success(payments);
        }
    }
}
