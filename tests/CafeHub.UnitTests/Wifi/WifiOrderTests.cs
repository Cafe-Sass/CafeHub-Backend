using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Wifi
{
    public sealed class WifiOrderTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid TableId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid PackageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");
        private const string Code = "Abcdefghijklmnopqrstuv";

        [Fact]
        public void APackageLineIsPricedOnceOnTheServer()
        {
            var line = new SubmitOrderLine(
                Guid.Empty, Guid.Empty, [], 2, 0m, [], PackageId);
            var packages = new Dictionary<Guid, WifiPackageResponse>
            {
                [PackageId] = PackageResponse(5m, WifiPackageStatus.Active),
            };

            var result = OrderPricing.Evaluate([line], new OrderCatalog([]), packages, null);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var priced = Assert.Single(result.Value!.Lines);
            Assert.Equal(10m, result.Value.TotalAmount);
            Assert.Equal(PackageId, priced.WifiPackageId);
            Assert.Equal(5m, priced.UnitPrice);
            Assert.Equal(2, priced.Quantity);
            Assert.Equal(60, priced.WifiDurationMinutes);
            Assert.Empty(priced.Addons);
        }

        [Fact]
        public void AnInactivePackageIsRefused()
        {
            var line = new SubmitOrderLine(
                Guid.Empty, Guid.Empty, [], 1, 0m, [], PackageId);
            var packages = new Dictionary<Guid, WifiPackageResponse>
            {
                [PackageId] = PackageResponse(5m, WifiPackageStatus.Inactive),
            };

            var result = OrderPricing.Evaluate([line], new OrderCatalog([]), packages, null);

            Assert.Equal(OrdersErrors.WifiPackageUnavailableCode, result.Error!.Code);
        }

        [Fact]
        public async Task TheFreePolicyRefusesTheWifiOnlyPurchase()
        {
            var setup = new Setup(WifiPolicy.Free, voucherAvailable: true);
            var handler = setup.WifiHandler();

            var result = await handler.Handle(setup.Command(), default);

            Assert.Equal(OrdersErrors.WifiPolicyNotPaidCode, result.Error!.Code);
            Assert.Empty(setup.Orders.Added);
        }

        [Fact]
        public async Task AnEmptyVoucherPoolRefusesTheWifiOnlyPurchase()
        {
            var setup = new Setup(WifiPolicy.Paid, voucherAvailable: false);
            var handler = setup.WifiHandler();

            var result = await handler.Handle(setup.Command(), default);

            Assert.Equal(OrdersErrors.WifiVoucherUnavailableCode, result.Error!.Code);
            Assert.Empty(setup.Orders.Added);
        }

        [Fact]
        public async Task AWifiOnlyOrderHasNoTableIsAcceptedAndGetsItsOwnBill()
        {
            var setup = new Setup(WifiPolicy.Paid, voucherAvailable: true);
            var handler = setup.WifiHandler();

            var result = await handler.Handle(setup.Command(), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal("Accepted", result.Value!.Status);
            var order = Assert.Single(setup.Orders.Added);
            Assert.Null(order.TableId);
            Assert.Equal(OrderServiceType.WifiOnly, order.ServiceType);
            Assert.Equal(OrderStatus.Accepted, order.Status);
            Assert.NotNull(order.BillId);
            var bill = Assert.Single(setup.Bills.Bills);
            Assert.Null(bill.TableId);
            Assert.Equal(bill.Id, order.BillId);
            var item = Assert.Single(order.Items);
            Assert.Equal(PackageId, item.WifiPackageId);
            Assert.Null(item.ProductId);
            Assert.Equal(60, item.WifiDurationMinutes);
            Assert.Equal(5m, item.UnitPrice);
            Assert.Equal(5m, order.TotalAmount);
            Assert.Equal(1, setup.Vouchers.IssueCalls);
        }

        [Fact]
        public async Task ARepeatWithTheSameKeyReturnsTheSameOrder()
        {
            var setup = new Setup(WifiPolicy.Paid, voucherAvailable: true);
            var handler = setup.WifiHandler();

            var first = await handler.Handle(setup.Command(), default);
            var repeat = await handler.Handle(setup.Command(), default);

            Assert.True(first.IsSuccess, first.Error?.Code);
            Assert.True(repeat.IsSuccess, repeat.Error?.Code);
            Assert.Equal(first.Value!.PublicReference, repeat.Value!.PublicReference);
            Assert.Single(setup.Orders.Added);
            Assert.Single(setup.Bills.Bills);
            Assert.Equal(1, setup.Vouchers.IssueCalls);
        }

        [Fact]
        public void TheModelCarriesTheWifiOrderChecks()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;
            using var context = new TenantDbContext(options);
            var model = context.GetService<IDesignTimeModel>().Model;

            var orders = model.FindEntityType(typeof(Order))!;
            Assert.True(orders.FindProperty(nameof(Order.TableId))!.IsNullable);
            Assert.Contains(
                orders.GetCheckConstraints(),
                check => check.Name == "ck_orders_service_table");
            Assert.Contains(
                orders.GetCheckConstraints(),
                check => check.Name == "ck_orders_service_type"
                    && check.Sql.Contains("WifiOnly", StringComparison.Ordinal));

            var items = model.FindEntityType(typeof(OrderItem))!;
            Assert.Contains(
                items.GetCheckConstraints(),
                check => check.Name == "ck_order_items_product_or_wifi");
            Assert.Contains(
                items.GetCheckConstraints(),
                check => check.Name == "ck_order_items_wifi_snapshot");

            var bills = model.FindEntityType(typeof(Bill))!;
            Assert.True(bills.FindProperty(nameof(Bill.TableId))!.IsNullable);
        }

        [Fact]
        public async Task TheTableSubmissionAcceptsAPackageLine()
        {
            var setup = new Setup(WifiPolicy.Paid, voucherAvailable: true);
            var handler = setup.TableHandler();

            var command = new SubmitOrderCommand
            {
                IdempotencyKey = "table-key",
                Code = Code,
                ServiceType = OrderServiceType.DineIn,
                GuestDisplayName = "Sara",
                Lines = [new SubmitOrderLine(Guid.Empty, Guid.Empty, [], 1, 0m, [], PackageId)],
            };

            var result = await handler.Handle(command, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            var order = Assert.Single(setup.Orders.Added);
            Assert.Equal(TableId, order.TableId);
            Assert.Equal(OrderServiceType.DineIn, order.ServiceType);
            var item = Assert.Single(order.Items);
            Assert.Equal(PackageId, item.WifiPackageId);
            Assert.Equal(5m, order.TotalAmount);
            Assert.Equal(0, setup.Vouchers.IssueCalls);
        }

        private static WifiPackageResponse PackageResponse(decimal price, WifiPackageStatus status) =>
            new(
                PackageId,
                "One Hour",
                60,
                1m,
                "Unlimited",
                null,
                price,
                0,
                status.ToString(),
                "1");

        private sealed class Setup
        {
            public Setup(WifiPolicy policy, bool voucherAvailable)
            {
                Package = PackageResponse(5m, WifiPackageStatus.Active);
                var qr = new WifiQrContext(BranchId, policy, null, [
                    WifiPackage.Create(
                        PackageId, "One Hour", 60, 1m, WifiDataMode.Unlimited, null, 5m, 0, Now, null),
                ]);
                WifiQr = new FakeWifiQrStore(qr);
                Vouchers = new FakeWifiVoucherStore(voucherAvailable);
                Packages = new FakeWifiPackageStore(Package);
                Orders = new FakeOrderStore();
                Bills = new FakeBillStore();
                Allocator = new FakeAllocator();
                References = new FakeReferenceGenerator();
            }

            public WifiPackageResponse Package { get; }

            public FakeWifiQrStore WifiQr { get; }

            public FakeWifiVoucherStore Vouchers { get; }

            public FakeWifiPackageStore Packages { get; }

            public FakeOrderStore Orders { get; }

            public FakeBillStore Bills { get; }

            public FakeAllocator Allocator { get; }

            public FakeReferenceGenerator References { get; }

            public SubmitWifiOrderCommand Command() => new()
            {
                IdempotencyKey = "wifi-key",
                Code = Code,
                PackageId = PackageId,
                GuestDisplayName = "Sara",
            };

            public SubmitWifiOrderCommandHandler WifiHandler() =>
                new(
                    WifiQr,
                    Vouchers,
                    Orders,
                    Allocator,
                    References,
                    Bills,
                    new OrdersOptions(),
                    new FixedClock());

            public SubmitOrderCommandHandler TableHandler() =>
                new(
                    new FakeSender(new GuestQrContext("Cafe", BranchId, "Downtown", TableId, 5)),
                    Orders,
                    Allocator,
                    References,
                    Bills,
                    Packages,
                    new FakeWifiSettingsStore(WifiPolicy.Paid),
                    Vouchers,
                    new OrdersOptions(),
                    new FixedClock(),
                    new FakeAuditWriter());
        }

        private sealed class FakeSender(GuestQrContext context) : ISender
        {
            public Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request,
                CancellationToken cancellationToken = default)
            {
                if (request is ResolveGuestQrQuery && typeof(TResponse) == typeof(Result<GuestQrContext>))
                {
                    return Task.FromResult((TResponse)(object)Result.Success(context));
                }

                throw new NotSupportedException("Only the QR resolution query is handled.");
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("Only the typed send is used.");

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                throw new NotSupportedException("Only the response typed send is used.");

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("Streams are not used.");

            public IAsyncEnumerable<object?> CreateStream(
                object request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("Streams are not used.");
        }

        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class FakeWifiQrStore(WifiQrContext context) : IWifiQrStore
        {
            public Task<Result<WifiQrData>> GetOrCreateAsync(
                Guid? branchIdScope,
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not manage the QR.");

            public Task<Result<WifiQrData>> RegenerateAsync(
                Guid? branchIdScope,
                Guid branchId,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not manage the QR.");

            public Task<WifiQrContext?> ResolveAsync(string code, CancellationToken cancellationToken) =>
                Task.FromResult<WifiQrContext?>(code == Code ? context : null);
        }

        private sealed class FakeWifiVoucherStore(bool hasAvailable) : IWifiVoucherStore
        {
            public int IssueCalls { get; private set; }

            public Task<Result> IssueForOrderAsync(
                Order order,
                Guid? actorUserId,
                CancellationToken cancellationToken)
            {
                IssueCalls++;
                return Task.FromResult(Result.Success());
            }

            public Task<bool> HasAvailableAsync(Guid packageId, CancellationToken cancellationToken) =>
                Task.FromResult(hasAvailable && packageId == PackageId);

            public Task<IReadOnlySet<string>> FindExistingCodesAsync(
                Guid packageId,
                IReadOnlyCollection<string> codes,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not import vouchers.");

            public Task<Result> InsertImportedAsync(
                Guid packageId,
                IReadOnlyList<WifiVoucher> vouchers,
                WifiVoucherImportResult counts,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not import vouchers.");

            public Task<WifiVoucherSummary> GetSummaryAsync(
                Guid packageId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not read voucher counts.");

            public Task<Result> VoidAsync(
                Guid packageId,
                string code,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not void vouchers.");
        }

        private sealed class FakeWifiPackageStore(WifiPackageResponse package) : IWifiPackageStore
        {
            public Task<WifiPackageListPage> ListAsync(
                WifiPackageListRequest request,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not list packages.");

            public Task<WifiPackageResponse?> GetAsync(Guid packageId, CancellationToken cancellationToken) =>
                Task.FromResult(packageId == PackageId ? package : null);

            public Task<bool> ActiveNameExistsAsync(
                string name,
                Guid? excludePackageId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not check names.");

            public Task<Result<WifiPackageResponse>> CreateAsync(
                WifiPackage package,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not create packages.");

            public Task<Result<WifiPackageResponse>> UpdateAsync(
                Guid packageId,
                string concurrencyToken,
                string name,
                int durationMinutes,
                decimal? speedMbps,
                WifiDataMode dataMode,
                int? quotaMb,
                decimal price,
                int sortOrder,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not update packages.");

            public Task<Result<WifiPackageResponse>> ChangeStatusAsync(
                Guid packageId,
                WifiPackageStatus status,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not change packages.");
        }

        private sealed class FakeWifiSettingsStore(WifiPolicy policy) : IWifiSettingsStore
        {
            public Task<WifiSettingsResponse> GetAsync(Guid? branchId, CancellationToken cancellationToken) =>
                Task.FromResult(new WifiSettingsResponse(policy, "1", []));

            public Task<Result<WifiSettingsResponse>> UpdatePolicyAsync(
                WifiPolicy requestedPolicy,
                string concurrencyToken,
                Guid actorUserId,
                Guid? branchId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not update the policy.");

            public Task<Result<WifiNetworkResponse?>> SetNetworkAsync(
                Guid? branchIdScope,
                Guid branchId,
                string? ssid,
                WifiSecurity? security,
                string? password,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not update a network.");
        }

        private sealed class FakeOrderStore : IOrderStore
        {
            public List<Order> Added { get; } = [];

            public StoredOrder? Existing { get; set; }

            public Task<OrderCatalog> LoadCatalogAsync(
                Guid branchId,
                IReadOnlyCollection<Guid> productIds,
                CancellationToken cancellationToken) =>
                Task.FromResult(new OrderCatalog([]));

            public Task<Result<SubmitOrderResult>> AddAsync(
                Order order,
                CancellationToken cancellationToken)
            {
                Added.Add(order);
                Existing = new StoredOrder(
                    order.RequestHash,
                    new SubmitOrderResult(
                        order.OrderNumber,
                        order.PublicReference,
                        order.Status.ToString(),
                        order.TotalAmount,
                        order.ResponseDeadline));
                return Task.FromResult(Result.Success(Existing.Result));
            }

            public Task<StoredOrder?> FindByIdempotencyKeyAsync(
                Guid branchId,
                string idempotencyKey,
                CancellationToken cancellationToken) =>
                Task.FromResult(Existing);
        }

        private sealed class FakeAllocator : IOrderNumberAllocator
        {
            private int number;

            public Task<OrderNumberAllocation> AllocateAsync(
                Guid branchId,
                CancellationToken cancellationToken) =>
                Task.FromResult(new OrderNumberAllocation(
                    ++number, new DateOnly(2026, 11, 24)));
        }

        private sealed class FakeReferenceGenerator : IOrderPublicReferenceGenerator
        {
            private int count;

            public string Generate() => $"Abcdefghijklmnopqrstu{count++ % 10}";
        }

        private sealed class FakeBillStore : IBillStore
        {
            public List<Bill> Bills { get; } = [];

            public Task<Result<Bill>> ResolveOpenBillAsync(
                Guid branchId,
                Guid? tableId,
                DateTimeOffset now,
                Guid? actorId,
                CancellationToken cancellationToken)
            {
                var bill = Bill.Create(
                    Guid.CreateVersion7(), branchId, tableId, "Zyxwvutsrqponmlkjihgfe", now, actorId);
                Bills.Add(bill);
                return Task.FromResult(Result.Success(bill));
            }

            public Task<BillSettlement?> FindForCloseAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not close bills.");

            public Task<Result> SaveCloseAsync(
                Bill bill,
                decimal remainingAmount,
                Guid actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The order flow does not close bills.");
        }

        private sealed class FakeAuditWriter : ITenantAuditWriter
        {
            public Task<Result> WriteAsync(
                TenantAuditEntry entry,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Success());
        }
    }
}
