using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class AcceptOrderCommandHandler(
        ISender sender,
        IOrderDecisionStore decisionStore,
        IBillStore billStore,
        ICurrentTenantUser currentUser,
        IStationScope stationScope,
        TimeProvider timeProvider,
        ITenantAuditWriter auditWriter)
        : ICommandHandler<AcceptOrderCommand, OrderDecisionResult>
    {
        private const string TargetType = "orders";

        public async Task<Result<OrderDecisionResult>> Handle(
            AcceptOrderCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            await sender.Send(new ExpireStaleOrdersCommand(null, command.OrderId), cancellationToken);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OrderDecisionResult>(scope.Error!);
            }

            IReadOnlyList<Guid>? visibleStationIds = null;
            if (scope.Value is not null && currentUser.UserId is { } userId)
            {
                visibleStationIds = await stationScope.GetVisibleStationIdsAsync(userId, cancellationToken);
            }

            var order = await decisionStore.FindDecisionTargetAsync(scope.Value, command.OrderId, cancellationToken);
            if (order is null
                || (visibleStationIds is not null && !order.Items.Any(item =>
                    item.StationId is null || visibleStationIds.Contains(item.StationId.Value))))
            {
                return Result.Failure<OrderDecisionResult>(OrdersErrors.NotFound());
            }

            if (order.Status is not OrderStatus.Pending)
            {
                return Result.Failure<OrderDecisionResult>(OrdersErrors.StatusConflict(order.Status));
            }

            var now = timeProvider.GetUtcNow();

            order.Accept(currentUser.UserId!.Value, now);

            var bill = await billStore.ResolveOpenBillAsync(
                order.BranchId,
                order.TableId,
                now,
                currentUser.UserId,
                cancellationToken);
            if (bill.IsFailure)
            {
                return Result.Failure<OrderDecisionResult>(bill.Error!);
            }

            order.AttachToBill(bill.Value.Id);

            var save = await decisionStore.SaveDecisionAsync(order, cancellationToken);
            if (save.IsFailure)
            {
                return Result.Failure<OrderDecisionResult>(save.Error!);
            }

            var auditResult = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "orders.accepted.succeeded",
                TargetType = TargetType,
                TargetId = order.Id,
                ActorUserId = currentUser.UserId,
                Changes = TenantAuditChanges.Update(
                [
                    new TenantAuditChanges.FieldChange(
                        nameof(Order.Status),
                        OrderStatus.Pending.ToString(),
                        order.Status.ToString()),
                ]),
            }, cancellationToken);
            if (auditResult.IsFailure)
            {
                return Result.Failure<OrderDecisionResult>(auditResult.Error!);
            }

            return Result.Success(new OrderDecisionResult(
                order.Id,
                order.OrderNumber,
                order.Status.ToString(),
                order.RespondedAt!.Value));
        }
    }
}
