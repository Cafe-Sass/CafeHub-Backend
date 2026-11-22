using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.Application.Orders
{
    public sealed class SubmitOrderCommandHandler(
        ISender sender,
        IOrderStore store,
        IOrderNumberAllocator allocator,
        IOrderPublicReferenceGenerator referenceGenerator,
        OrdersOptions options,
        TimeProvider timeProvider)
        : ICommandHandler<SubmitOrderCommand, SubmitOrderResult>
    {
        private const int MaximumReferenceAttempts = 2;

        public async Task<Result<SubmitOrderResult>> Handle(
            SubmitOrderCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var context = await sender.Send(new ResolveGuestQrQuery(command.Code), cancellationToken);
            if (context.IsFailure)
            {
                return Result.Failure<SubmitOrderResult>(context.Error!);
            }

            var qr = context.Value;
            var catalog = await store.LoadCatalogAsync(
                qr.BranchId,
                command.Lines.Select(line => line.ProductId).Distinct().ToArray(),
                cancellationToken);
            var priced = OrderPricing.Evaluate(command.Lines, catalog);
            if (priced.IsFailure)
            {
                return Result.Failure<SubmitOrderResult>(priced.Error!);
            }

            var allocation = await allocator.AllocateAsync(qr.BranchId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var placedByUserId = command.Source == OrderSource.Captain ? command.ActingStaffUserId : null;

            for (var attempt = 0; attempt < MaximumReferenceAttempts; attempt++)
            {
                var order = CreateOrder(command, qr, allocation, priced.Value, placedByUserId, now);
                var added = await store.AddAsync(order, cancellationToken);
                if (added.IsSuccess)
                {
                    return added;
                }

                if (added.Error!.Code != OrdersErrors.ReferenceConflictCode)
                {
                    return added;
                }
            }

            return Result.Failure<SubmitOrderResult>(OrdersErrors.ReferenceConflict());
        }

        private Order CreateOrder(
            SubmitOrderCommand command,
            GuestQrContext qr,
            OrderNumberAllocation allocation,
            PricedOrder priced,
            Guid? placedByUserId,
            DateTimeOffset now)
        {
            var items = priced.Lines
                .Select(line => OrderItem.Create(
                    Guid.CreateVersion7(),
                    line.LineNumber,
                    line.ProductId,
                    line.SizeId,
                    line.StationId,
                    line.ProductName,
                    line.SizeName,
                    line.UnitPrice,
                    line.Quantity,
                    line.Addons
                        .Select(addon => OrderItemAddon.Create(
                            Guid.CreateVersion7(),
                            addon.ProductAddonId,
                            addon.Name,
                            addon.Price,
                            now,
                            placedByUserId))
                        .ToArray(),
                    now,
                    placedByUserId))
                .ToArray();

            return Order.Create(
                Guid.CreateVersion7(),
                qr.BranchId,
                qr.TableId,
                allocation.Number,
                allocation.BusinessDate,
                command.ServiceType,
                command.Source,
                command.GuestDisplayName,
                command.GuestDeviceId,
                placedByUserId,
                referenceGenerator.Generate(),
                command.IdempotencyKey!,
                null,
                now,
                now.AddSeconds(options.ResponseWindowSeconds),
                items,
                placedByUserId);
        }
    }
}
