using System.Globalization;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.Application.Orders
{
    public sealed class SubmitOrderCommandHandler(
        ISender sender,
        IOrderStore store,
        IOrderNumberAllocator allocator,
        IOrderPublicReferenceGenerator referenceGenerator,
        IBillStore billStore,
        IWifiPackageStore wifiPackages,
        IWifiSettingsStore wifiSettings,
        IWifiVoucherStore wifiVouchers,
        OrdersOptions options,
        TimeProvider timeProvider,
        ITenantAuditWriter auditWriter)
        : ICommandHandler<SubmitOrderCommand, SubmitOrderResult>
    {
        private const int MaximumReferenceAttempts = 2;
        private const string TargetType = "orders";
        private const string AssistedCreatedAction = "orders.assisted_created.succeeded";

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
            var requestHash = OrderRequestHash.Compute(
                qr.TableId, command.ServiceType, command.Source, command.GuestDisplayName, command.Lines);

            var replay = await store.FindByIdempotencyKeyAsync(
                qr.BranchId, command.IdempotencyKey!, cancellationToken);
            if (replay is not null)
            {
                return string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal)
                    ? Result.Success(replay.Result)
                    : Result.Failure<SubmitOrderResult>(IdempotencyErrors.KeyReused());
            }

            var catalog = await store.LoadCatalogAsync(
                qr.BranchId,
                command.Lines.Select(line => line.ProductId).Distinct().ToArray(),
                cancellationToken);
            var packageCatalog = new Dictionary<Guid, WifiPackageResponse>();
            foreach (var packageId in command.Lines
                         .Select(line => line.WifiPackageId)
                         .OfType<Guid>()
                         .Distinct())
            {
                var package = await wifiPackages.GetAsync(packageId, cancellationToken);
                if (package is not null)
                {
                    packageCatalog[packageId] = package;
                }
            }

            var priced = OrderPricing.Evaluate(command.Lines, catalog, packageCatalog, command.ExpectedTotal);
            if (priced.IsFailure)
            {
                return Result.Failure<SubmitOrderResult>(priced.Error!);
            }

            if (packageCatalog.Count > 0)
            {
                var settings = await wifiSettings.GetAsync(null, cancellationToken);
                if (settings.Policy != WifiPolicy.Paid)
                {
                    return Result.Failure<SubmitOrderResult>(OrdersErrors.WifiPolicyNotPaid());
                }

                foreach (var packageId in packageCatalog.Keys)
                {
                    if (!await wifiVouchers.HasAvailableAsync(packageId, cancellationToken))
                    {
                        return Result.Failure<SubmitOrderResult>(OrdersErrors.WifiVoucherUnavailable());
                    }
                }
            }

            var allocation = await allocator.AllocateAsync(qr.BranchId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var placedByUserId = command.Source == OrderSource.Captain ? command.ActingStaffUserId : null;

            for (var attempt = 0; attempt < MaximumReferenceAttempts; attempt++)
            {
                var order = CreateOrder(command, qr, allocation, priced.Value, placedByUserId, now, requestHash);
                var added = await store.AddAsync(order, cancellationToken);
                if (added.IsSuccess)
                {
                    if (order.Status == OrderStatus.Accepted)
                    {
                        var issued = await wifiVouchers.IssueForOrderAsync(order, placedByUserId, cancellationToken);
                        if (issued.IsFailure)
                        {
                            return Result.Failure<SubmitOrderResult>(issued.Error!);
                        }
                    }

                    if (placedByUserId is not null)
                    {
                        var auditResult = await auditWriter.WriteAsync(
                            new TenantAuditEntry
                            {
                                Action = AssistedCreatedAction,
                                TargetType = TargetType,
                                TargetId = order.Id,
                                ActorUserId = placedByUserId,
                                Changes = TenantAuditChanges.Create(
                                [
                                    new TenantAuditChanges.FieldChange(
                                        nameof(Order.OrderNumber),
                                        null,
                                        order.OrderNumber.ToString(CultureInfo.InvariantCulture),
                                        BeforePresent: false),
                                ]),
                            },
                            cancellationToken);
                        if (auditResult.IsFailure)
                        {
                            return Result.Failure<SubmitOrderResult>(auditResult.Error!);
                        }
                    }

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
            DateTimeOffset now,
            string requestHash)
        {
            var items = priced.Lines
                .Select(line => line.WifiPackageId is { } packageId
                    ? OrderItem.CreateWifiLine(
                        Guid.CreateVersion7(),
                        line.LineNumber,
                        packageId,
                        line.ProductName,
                        line.WifiDurationMinutes!.Value,
                        line.WifiSpeedMbps,
                        line.UnitPrice,
                        line.Quantity,
                        now,
                        placedByUserId)
                    : OrderItem.Create(
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

            var order = Order.Create(
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
                requestHash,
                now,
                now.AddSeconds(options.ResponseWindowSeconds),
                items,
                placedByUserId);

            if (placedByUserId is { } captainId)
            {
                order.Accept(captainId, now);
            }

            return order;
        }
    }
}
