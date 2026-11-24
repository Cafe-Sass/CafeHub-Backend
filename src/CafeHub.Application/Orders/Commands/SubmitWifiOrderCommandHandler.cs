using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Tables;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders.Commands
{
    public sealed class SubmitWifiOrderCommandHandler(
        IWifiQrStore wifiQr,
        IWifiVoucherStore wifiVouchers,
        IOrderStore store,
        IOrderNumberAllocator allocator,
        IOrderPublicReferenceGenerator referenceGenerator,
        IBillStore billStore,
        OrdersOptions options,
        TimeProvider timeProvider)
        : ICommandHandler<SubmitWifiOrderCommand, SubmitOrderResult>
    {
        public async Task<Result<SubmitOrderResult>> Handle(
            SubmitWifiOrderCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var wifi = await wifiQr.ResolveAsync(command.Code!, cancellationToken);
            if (wifi is null)
            {
                return Result.Failure<SubmitOrderResult>(GuestQrErrors.NotFound());
            }

            if (wifi.Policy != WifiPolicy.Paid)
            {
                return Result.Failure<SubmitOrderResult>(OrdersErrors.WifiPolicyNotPaid());
            }

            var package = wifi.Packages
                .FirstOrDefault(candidate => candidate.Id == command.PackageId
                    && candidate.Status == WifiPackageStatus.Active);
            if (package is null)
            {
                return Result.Failure<SubmitOrderResult>(OrdersErrors.WifiPackageUnavailable());
            }

            if (!await wifiVouchers.HasAvailableAsync(package.Id, cancellationToken))
            {
                return Result.Failure<SubmitOrderResult>(OrdersErrors.WifiVoucherUnavailable());
            }

            var line = new SubmitOrderLine(
                Guid.Empty,
                Guid.Empty,
                [],
                1,
                package.Price,
                [],
                package.Id);
            var requestHash = OrderRequestHash.Compute(
                null,
                OrderServiceType.WifiOnly,
                OrderSource.Guest,
                command.GuestDisplayName,
                [line]);

            var replay = await store.FindByIdempotencyKeyAsync(
                wifi.BranchId,
                command.IdempotencyKey!,
                cancellationToken);
            if (replay is not null)
            {
                return string.Equals(replay.RequestHash, requestHash, StringComparison.Ordinal)
                    ? Result.Success(replay.Result)
                    : Result.Failure<SubmitOrderResult>(IdempotencyErrors.KeyReused());
            }

            var allocation = await allocator.AllocateAsync(wifi.BranchId, cancellationToken);
            var now = timeProvider.GetUtcNow();
            var item = OrderItem.CreateWifiLine(
                Guid.CreateVersion7(),
                1,
                package.Id,
                package.Name,
                package.DurationMinutes,
                package.SpeedMbps,
                package.Price,
                1,
                now,
                null);
            var order = Order.Create(
                Guid.CreateVersion7(),
                wifi.BranchId,
                null,
                allocation.Number,
                allocation.BusinessDate,
                OrderServiceType.WifiOnly,
                OrderSource.Guest,
                command.GuestDisplayName,
                null,
                null,
                referenceGenerator.Generate(),
                command.IdempotencyKey!,
                requestHash,
                now,
                now.AddSeconds(options.ResponseWindowSeconds),
                [item],
                null);
            order.AcceptAutomatically(now);

            var bill = await billStore.ResolveOpenBillAsync(
                wifi.BranchId,
                null,
                now,
                null,
                cancellationToken);
            if (bill.IsFailure)
            {
                return Result.Failure<SubmitOrderResult>(bill.Error!);
            }

            order.AttachToBill(bill.Value.Id);
            var added = await store.AddAsync(order, cancellationToken);
            if (added.IsFailure)
            {
                return added;
            }

            var issued = await wifiVouchers.IssueForOrderAsync(order, null, cancellationToken);
            return issued.IsFailure
                ? Result.Failure<SubmitOrderResult>(issued.Error!)
                : added;
        }
    }
}
