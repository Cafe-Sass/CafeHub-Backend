using System.Reflection;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.UnitTests.Orders
{
    public sealed class CaptainAssistedOrderTests
    {
        private const string Code = "Abcdefghijklmnopqrstuv";
        private const string Key = "idempotency-key-1234";
        private const string GuestName = "Sara";

        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid TableId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid CaptainId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid SizeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid AddonId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

        private readonly DispatchSender sender = new();
        private readonly FakeOrderStore store = new() { Catalog = Catalog() };
        private readonly FakeAllocator allocator = new(7);
        private readonly FakeReferenceGenerator references = new();
        private readonly FakeAuditWriter audit = new();
        private readonly FakeTableStore tables = new();
        private readonly FakeTenantUser captain = new()
        {
            UserId = CaptainId,
            AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId },
        };

        [Fact]
        public async Task ACaptainCreatesAnAcceptedOrderForAnActiveTableOfTheBranch()
        {
            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatus.Accepted.ToString(), result.Value.Status);
            Assert.Equal(TableId, Assert.Single(tables.Lookups));
            var order = Assert.Single(store.Added);
            Assert.Equal(BranchId, order.BranchId);
            Assert.Equal(TableId, order.TableId);
            Assert.Equal(OrderSource.Captain, order.Source);
            Assert.Equal(CaptainId, order.PlacedByUserId);
            Assert.Equal(CaptainId, order.RespondedByUserId);
            Assert.Equal(Now, order.RespondedAt);
            Assert.Null(order.GuestDeviceId);
            Assert.Equal(GuestName, order.GuestDisplayName);
            Assert.Equal(Code, sender.LastCommand!.Code);
        }

        [Fact]
        public async Task AnAssistedOrderIsAuditedWithOnlyTheOrderNumber()
        {
            await CreateHandler().Handle(Valid(), default);

            var entry = Assert.Single(audit.Entries);
            Assert.Equal("orders.assisted_created.succeeded", entry.Action);
            Assert.Equal("orders", entry.TargetType);
            Assert.Equal(Assert.Single(store.Added).Id, entry.TargetId);
            Assert.Equal(CaptainId, entry.ActorUserId);
            var change = Assert.Single(entry.Changes!.Fields);
            Assert.Equal(nameof(Order.OrderNumber), change.Name);
            Assert.Equal("7", change.After);
        }

        [Fact]
        public async Task ATableOfAnotherBranchReturnsTheUniformNotFound()
        {
            tables.Table = new OrderTable(OtherBranchId, Code);

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
            Assert.Equal(0, sender.InnerCalls);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task AnInactiveTableReturnsTheUniformNotFound()
        {
            tables.Table = null;

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(0, sender.InnerCalls);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task AStaffMemberWithoutAnActiveBranchIsForbidden()
        {
            var unassigned = new FakeTenantUser
            {
                UserId = CaptainId,
                AccountAccess = new TenantAccountAccess(true, null, false),
            };

            var result = await CreateHandler(unassigned).Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
            Assert.Empty(tables.Lookups);
            Assert.Equal(0, sender.InnerCalls);
        }

        [Fact]
        public async Task AChangedPriceIsRefusedByTheSharedSubmissionRules()
        {
            store.Catalog = Catalog(unitPrice: 11m);

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.PriceChangedCode, result.Error!.Code);
            Assert.Empty(store.Added);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task AnUnavailableProductIsRefusedByTheSharedSubmissionRules()
        {
            store.Catalog = Catalog(status: ProductStatus.Inactive);

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.ProductUnavailableCode, result.Error!.Code);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task ARepeatedKeyReturnsTheStoredOrderWithoutAllocatingOrAuditing()
        {
            store.Existing = new StoredOrder(
                OrderRequestHash.Compute(
                    TableId, OrderServiceType.DineIn, OrderSource.Captain, GuestName, Valid().Lines),
                new SubmitOrderResult(3, "reference0000000000003", OrderStatus.Accepted.ToString(), 22m, Now));

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.OrderNumber);
            Assert.Empty(store.Added);
            Assert.Equal(0, allocator.Calls);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public void TheTableIsRequired()
        {
            var validation = new SubmitAssistedOrderCommandValidator()
                .Validate(new SubmitAssistedOrderCommand { IdempotencyKey = Key });

            Assert.False(validation.IsValid);
            Assert.Contains(
                validation.Errors,
                error => error.PropertyName == nameof(SubmitAssistedOrderCommand.TableId));
        }

        [Fact]
        public void TheCommandIsSensitiveSoTheGuestNameNeverReachesTheLogs()
        {
            Assert.IsAssignableFrom<ISensitiveRequest>(Valid());
        }

        [Fact]
        public void SubmitAssisted_IsAnAuthorizedPostActionWithTheIdempotencyHeader()
        {
            var action = typeof(TenantOrdersController).GetMethod(nameof(TenantOrdersController.SubmitAssisted))!;

            Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
            Assert.Equal("assisted", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            var authorize = Assert.IsType<TenantAuthorizeAttribute>(action.GetCustomAttribute<TenantAuthorizeAttribute>());
            Assert.Equal(TenantPermissions.OrdersCaptain, authorize.Policy);
            var header = action.GetParameters().Single(parameter => parameter.Name == "idempotencyKey");
            Assert.Equal("Idempotency-Key", header.GetCustomAttribute<FromHeaderAttribute>()!.Name);
        }

        [Fact]
        public void TheOrdersCaptainPermissionIsGrantableToStaff()
        {
            Assert.Equal("orders.captain", TenantPermissions.OrdersCaptain);
            Assert.Contains(TenantPermissions.OrdersCaptain, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.OrdersCaptain));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.OrdersCaptain));
        }

        private SubmitAssistedOrderCommandHandler CreateHandler(ICurrentTenantUser? user = null)
        {
            sender.Inner = new SubmitOrderCommandHandler(
                sender, store, allocator, references, new FakeBillStore(), new FakeWifiPackageStore(), new FakeWifiSettingsStore(), new FakeWifiVoucherStore(), new OrdersOptions(), new FixedClock(), audit);
            return new SubmitAssistedOrderCommandHandler(sender, tables, user ?? captain);
        }

        private static SubmitAssistedOrderCommand Valid() => new()
        {
            IdempotencyKey = Key,
            TableId = TableId,
            ServiceType = OrderServiceType.DineIn,
            GuestDisplayName = GuestName,
            ExpectedTotal = 22m,
            Lines = [new SubmitOrderLine(ProductId, SizeId, [AddonId], 2, 10m, [2m])],
        };

        private static GuestQrContext Qr() => new("Cafe", BranchId, "Downtown", TableId, 5);

        private static OrderCatalog Catalog(
            ProductStatus status = ProductStatus.Active, decimal unitPrice = 10m) =>
            new([
                new OrderCatalogProduct(
                    ProductId,
                    "Latte",
                    Guid.Parse("44444444-4444-4444-4444-444444444444"),
                    status,
                    true,
                    true,
                    [new OrderCatalogSize(SizeId, "Large", unitPrice)],
                    [new OrderCatalogAddon(AddonId, "Oat milk", 2m)]),
            ]);

        private sealed class FixedClock : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => Now;
        }

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId { get; init; }

            public Guid? TenantId { get; init; } = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            public IReadOnlyCollection<string> Roles => ["Captain"];

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; }
        }

        private sealed class DispatchSender : ISender
        {
            public SubmitOrderCommandHandler? Inner { get; set; }

            public int InnerCalls { get; private set; }

            public SubmitOrderCommand? LastCommand { get; private set; }

            public async Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                if (request is ResolveGuestQrQuery)
                {
                    return (TResponse)(object)Result.Success(Qr());
                }

                if (request is SubmitOrderCommand command)
                {
                    InnerCalls++;
                    LastCommand = command;
                    return (TResponse)(object)await Inner!.Handle(command, cancellationToken);
                }

                throw new NotSupportedException("The assisted order sends only its own requests.");
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                throw new NotSupportedException("The assisted order sends only requests with a response.");

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The assisted order sends only typed requests.");

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The assisted order creates no streams.");

            public IAsyncEnumerable<object?> CreateStream(
                object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The assisted order creates no streams.");
        }

        private sealed class FakeTableStore : IOrderTableStore
        {
            public OrderTable? Table { get; set; } = new(BranchId, Code);

            public List<Guid> Lookups { get; } = [];

            public Task<OrderTable?> FindActiveTableAsync(Guid tableId, CancellationToken cancellationToken)
            {
                Lookups.Add(tableId);
                return Task.FromResult(Table);
            }
        }

        private sealed class FakeAllocator(int number) : IOrderNumberAllocator
        {
            public int Calls { get; private set; }

            public Task<OrderNumberAllocation> AllocateAsync(
                Guid branchId, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(new OrderNumberAllocation(number, new DateOnly(2026, 11, 23)));
            }
        }

        private sealed class FakeReferenceGenerator : IOrderPublicReferenceGenerator
        {
            public int Calls { get; private set; }

            public string Generate()
            {
                Calls++;
                var value = Calls.ToString().PadLeft(13, '0');
                return $"reference{value}";
            }
        }

        private sealed class FakeAuditWriter : ITenantAuditWriter
        {
            public List<TenantAuditEntry> Entries { get; } = [];

            public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken)
            {
                Entries.Add(entry);
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeBillStore : CafeHub.Application.Billing.Contracts.IBillStore
        {
            public Task<Result<Bill>> ResolveOpenBillAsync(
                Guid branchId,
                Guid? tableId,
                DateTimeOffset now,
                Guid? actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not resolve a bill.");

            public Task<CafeHub.Application.Billing.Contracts.BillSettlement?> FindForCloseAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not close bills.");

            public Task<Result> SaveCloseAsync(
                Bill bill,
                decimal remainingAmount,
                Guid actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not close bills.");
        }

        private sealed class FakeWifiPackageStore : IWifiPackageStore
        {
            public Task<WifiPackageListPage> ListAsync(
                WifiPackageListRequest request,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not list packages.");

            public Task<WifiPackageResponse?> GetAsync(Guid packageId, CancellationToken cancellationToken) =>
                Task.FromResult<WifiPackageResponse?>(null);

            public Task<bool> ActiveNameExistsAsync(
                string name,
                Guid? excludePackageId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not check names.");

            public Task<Result<WifiPackageResponse>> CreateAsync(
                WifiPackage package,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not create packages.");

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
                throw new NotSupportedException("The assisted submission does not update packages.");

            public Task<Result<WifiPackageResponse>> ChangeStatusAsync(
                Guid packageId,
                WifiPackageStatus status,
                string concurrencyToken,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not change packages.");
        }

        private sealed class FakeWifiSettingsStore : IWifiSettingsStore
        {
            public Task<WifiSettingsResponse> GetAsync(Guid? branchId, CancellationToken cancellationToken) =>
                Task.FromResult(new WifiSettingsResponse(WifiPolicy.Free, "1", []));

            public Task<Result<WifiSettingsResponse>> UpdatePolicyAsync(
                WifiPolicy policy,
                string concurrencyToken,
                Guid actorUserId,
                Guid? branchId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not update the policy.");

            public Task<Result<WifiNetworkResponse?>> SetNetworkAsync(
                Guid? branchIdScope,
                Guid branchId,
                string? ssid,
                WifiSecurity? security,
                string? password,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not update a network.");
        }

        private sealed class FakeWifiVoucherStore : IWifiVoucherStore
        {
            public Task<Result> IssueForOrderAsync(
                Order order,
                Guid? actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Success());

            public Task<bool> HasAvailableAsync(Guid packageId, CancellationToken cancellationToken) =>
                Task.FromResult(false);

            public Task<IReadOnlySet<string>> FindExistingCodesAsync(
                Guid packageId,
                IReadOnlyCollection<string> codes,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not import vouchers.");

            public Task<Result> InsertImportedAsync(
                Guid packageId,
                IReadOnlyList<WifiVoucher> vouchers,
                WifiVoucherImportResult counts,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not import vouchers.");

            public Task<WifiVoucherSummary> GetSummaryAsync(
                Guid packageId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not read voucher counts.");

            public Task<Result> VoidAsync(
                Guid packageId,
                string code,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The assisted submission does not void vouchers.");
        }

        private sealed class FakeOrderStore : IOrderStore
        {
            public OrderCatalog Catalog { get; set; } = new([]);

            public List<Order> Added { get; } = [];

            public StoredOrder? Existing { get; set; }

            public Task<OrderCatalog> LoadCatalogAsync(
                Guid branchId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
                Task.FromResult(Catalog);

            public Task<Result<SubmitOrderResult>> AddAsync(Order order, CancellationToken cancellationToken)
            {
                Added.Add(order);
                return Task.FromResult(Result.Success(new SubmitOrderResult(
                    order.OrderNumber,
                    order.PublicReference,
                    order.Status.ToString(),
                    order.TotalAmount,
                    order.ResponseDeadline)));
            }

            public Task<StoredOrder?> FindByIdempotencyKeyAsync(
                Guid branchId, string idempotencyKey, CancellationToken cancellationToken) =>
                Task.FromResult(Existing);
        }
    }
}
