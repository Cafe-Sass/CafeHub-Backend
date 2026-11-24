using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetReceiptQueryHandler(
        IReceiptQueryStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetReceiptQuery, ReceiptDetail>
    {
        public async Task<Result<ReceiptDetail>> Handle(
            GetReceiptQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<ReceiptDetail>(scope.Error!);
            }

            var receipt = await store.FindByIdAsync(scope.Value, query.ReceiptId, cancellationToken);
            return receipt is null
                ? Result.Failure<ReceiptDetail>(ReceiptErrors.NotFound())
                : Result.Success(ReceiptDetail.From(receipt));
        }
    }
}
