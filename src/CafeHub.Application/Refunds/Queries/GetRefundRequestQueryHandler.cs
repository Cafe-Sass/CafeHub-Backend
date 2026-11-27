using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;

namespace CafeHub.Application.Refunds.Queries
{
    public sealed class GetRefundRequestQueryHandler(
        IRefundRequestStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetRefundRequestQuery, RefundRequestDetail>
    {
        public async Task<Result<RefundRequestDetail>> Handle(
            GetRefundRequestQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<RefundRequestDetail>(scope.Error!);
            }

            var request = await store.FindAsync(scope.Value, query.RefundRequestId, cancellationToken);
            return request is null
                ? Result.Failure<RefundRequestDetail>(RefundRequestErrors.NotFound())
                : Result.Success(RefundRequestDetail.From(request));
        }
    }
}
