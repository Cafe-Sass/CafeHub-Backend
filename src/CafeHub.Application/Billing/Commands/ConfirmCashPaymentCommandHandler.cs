using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class ConfirmCashPaymentCommandHandler(
        IPaymentStore store,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<ConfirmCashPaymentCommand, ConfirmCashPaymentResult>
    {
        public async Task<Result<ConfirmCashPaymentResult>> Handle(
            ConfirmCashPaymentCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<ConfirmCashPaymentResult>(scope.Error!);
            }

            var settlement = await store.FindSettlementAsync(scope.Value, command.BillId, cancellationToken);
            if (settlement is null)
            {
                return Result.Failure<ConfirmCashPaymentResult>(BillingErrors.NotFound());
            }

            var stored = settlement.Payments.FirstOrDefault(
                payment => payment.IdempotencyKey == command.IdempotencyKey);
            if (stored is not null)
            {
                return Result.Success(BuildSettlementResult(settlement, stored));
            }

            if (settlement.Bill.Status is not BillStatus.Open)
            {
                return Result.Failure<ConfirmCashPaymentResult>(BillingErrors.BillClosed());
            }

            Order? linkedOrder = null;
            if (command.OrderId is { } orderId)
            {
                linkedOrder = settlement.AcceptedOrders.FirstOrDefault(order => order.Id == orderId);
                if (linkedOrder is null)
                {
                    return Result.Failure<ConfirmCashPaymentResult>(BillingErrors.OrderNotOnBill());
                }
            }

            var billTotal = Bill.ResolveTotalAmount(settlement.AcceptedOrders);
            var remaining = linkedOrder is null
                ? Payment.ResolveBillRemainingAmount(billTotal, settlement.Payments)
                : Payment.ResolveOrderRemainingAmount(linkedOrder, settlement.Payments);
            if (command.Amount > remaining)
            {
                return Result.Failure<ConfirmCashPaymentResult>(BillingErrors.AmountAboveRemaining());
            }

            var now = timeProvider.GetUtcNow();
            var payment = Payment.ConfirmCash(
                Guid.CreateVersion7(),
                settlement.Bill.Id,
                command.OrderId,
                command.Amount,
                currentUser.UserId!.Value,
                command.IdempotencyKey!,
                now,
                currentUser.UserId);

            var add = await store.AddAsync(payment, cancellationToken);
            if (add.IsFailure)
            {
                return Result.Failure<ConfirmCashPaymentResult>(add.Error!);
            }

            if (!ReferenceEquals(add.Value, payment))
            {
                var fresh = await store.FindSettlementAsync(scope.Value, command.BillId, cancellationToken);
                if (fresh is null)
                {
                    return Result.Failure<ConfirmCashPaymentResult>(BillingErrors.NotFound());
                }

                return Result.Success(BuildSettlementResult(fresh, add.Value));
            }

            var paid = Payment.ResolvePaidAmount(settlement.Payments) + add.Value.Amount;
            return Result.Success(new ConfirmCashPaymentResult(
                add.Value.Id,
                add.Value.Amount,
                add.Value.Method.ToString(),
                add.Value.Status.ToString(),
                add.Value.OrderId,
                paid,
                billTotal - paid,
                add.Value.ConfirmedAt!.Value));
        }

        private static ConfirmCashPaymentResult BuildSettlementResult(BillSettlement settlement, Payment payment)
        {
            var billTotal = Bill.ResolveTotalAmount(settlement.AcceptedOrders);
            var paid = Payment.ResolvePaidAmount(settlement.Payments);
            return new ConfirmCashPaymentResult(
                payment.Id,
                payment.Amount,
                payment.Method.ToString(),
                payment.Status.ToString(),
                payment.OrderId,
                paid,
                billTotal - paid,
                payment.ConfirmedAt!.Value);
        }
    }
}
