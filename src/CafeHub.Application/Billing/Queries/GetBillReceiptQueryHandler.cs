using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetBillReceiptQueryHandler(
        IReceiptQueryStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetBillReceiptQuery, ReceiptDetail>
    {
        public async Task<Result<ReceiptDetail>> Handle(
            GetBillReceiptQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<ReceiptDetail>(scope.Error!);
            }

            var receipt = await store.FindByBillAsync(scope.Value, query.BillId, cancellationToken);
            return receipt is null
                ? Result.Failure<ReceiptDetail>(ReceiptErrors.NotFound())
                : Result.Success(ReceiptDetail.From(receipt));
        }
    }
}
