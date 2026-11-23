using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Billing.Queries
{
    public sealed class GetBillQueryHandler(IBillQueryStore store, ICurrentTenantUser currentUser)
        : IQueryHandler<GetBillQuery, BillDetail>
    {
        public async Task<Result<BillDetail>> Handle(
            GetBillQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<BillDetail>(scope.Error!);
            }

            var settlement = await store.FindDetailAsync(scope.Value, query.BillId, cancellationToken);
            if (settlement is null)
            {
                return Result.Failure<BillDetail>(BillingErrors.NotFound());
            }

            return Result.Success(BillDetail.From(settlement));
        }
    }
}
