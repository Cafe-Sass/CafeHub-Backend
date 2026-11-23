using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class CloseBillCommandHandler(
        IBillStore billStore,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<CloseBillCommand, BillDetail>
    {
        public async Task<Result<BillDetail>> Handle(
            CloseBillCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<BillDetail>(scope.Error!);
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<BillDetail>(AuthErrors.Forbidden());
            }

            var settlement = await billStore.FindForCloseAsync(scope.Value, command.BillId, cancellationToken);
            if (settlement is null)
            {
                return Result.Failure<BillDetail>(BillingErrors.NotFound());
            }

            if (settlement.Bill.Status is BillStatus.Closed)
            {
                return Result.Success(BillDetail.From(settlement));
            }

            var remaining = Payment.ResolveBillRemainingAmount(
                Bill.ResolveTotalAmount(settlement.AcceptedOrders),
                settlement.Payments);
            settlement.Bill.Close(command.Reason, actorId, timeProvider.GetUtcNow());

            var saved = await billStore.SaveCloseAsync(
                settlement.Bill,
                remaining,
                actorId,
                cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure<BillDetail>(saved.Error!);
            }

            return Result.Success(BillDetail.From(settlement));
        }
    }
}
