using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetGuestReceiptQueryHandler(IReceiptQueryStore store)
        : IQueryHandler<GetGuestReceiptQuery, ReceiptDetail>
    {
        public async Task<Result<ReceiptDetail>> Handle(
            GetGuestReceiptQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (!Bill.IsValidPublicReference(query.BillReference))
            {
                return Result.Failure<ReceiptDetail>(ReceiptErrors.NotFound());
            }

            var receipt = await store.FindByBillReferenceAsync(
                query.BillReference!,
                cancellationToken);
            return receipt is null
                ? Result.Failure<ReceiptDetail>(ReceiptErrors.NotFound())
                : Result.Success(ReceiptDetail.From(receipt));
        }
    }
}
