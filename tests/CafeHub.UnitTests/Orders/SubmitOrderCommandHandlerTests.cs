using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Orders
{
    public sealed class SubmitOrderCommandHandlerTests
    {
        private const string Code = "Abcdefghijklmnopqrstuv";
        private const string Key = "idempotency-key-1234";

        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid TableId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid SizeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid AddonId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

        private readonly RecordingSender sender = new() { Response = Result.Success(Qr()) };
        private readonly FakeOrderStore store = new() { Catalog = Catalog() };
        private readonly FakeAllocator allocator = new(7);
        private readonly FakeReferenceGenerator references = new();
        private readonly FakeAuditWriter audit = new();

        [Fact]
        public async Task ASuccessfulSubmissionReturnsTheNumberedStoredOrder()
        {
            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(7, result.Value.OrderNumber);
            Assert.Equal("reference0000000000001", result.Value.PublicReference);
            Assert.Equal(OrderStatus.Pending.ToString(), result.Value.Status);
            Assert.Equal(22m, result.Value.TotalAmount);
            Assert.Equal(Now.AddSeconds(300), result.Value.ResponseDeadline);
        }

        [Fact]
        public async Task TheOrderTakesItsBranchAndTableFromTheQrAndNeverFromTheRequest()
        {
            await CreateHandler().Handle(Valid(), default);

            var order = Assert.Single(store.Added);
            Assert.Equal(BranchId, order.BranchId);
            Assert.Equal(TableId, order.TableId);
            Assert.DoesNotContain(
                typeof(SubmitOrderCommand).GetProperties(),
                property => property.Name.Contains("Table", StringComparison.Ordinal));
        }

        [Fact]
        public async Task TheStoredOrderSnapshotsEveryLineAndItsStation()
        {
            await CreateHandler().Handle(Valid(), default);

            var order = Assert.Single(store.Added);
            var item = Assert.Single(order.Items);
            Assert.Equal(1, item.LineNumber);
            Assert.Equal("Latte", item.ProductName);
            Assert.Equal("Large", item.SizeName);
            Assert.Equal(10m, item.UnitPrice);
            Assert.Equal(2, item.Quantity);
            Assert.NotNull(item.StationId);
            Assert.Equal("Oat milk", Assert.Single(item.Addons).AddonName);
            Assert.Equal(22m, order.TotalAmount);
        }

        [Fact]
        public async Task ARepeatedKeyWithTheSamePayloadReturnsTheStoredOrderWithoutAllocating()
        {
            store.Existing = new StoredOrder(
                OrderRequestHash.Compute(
                    TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", Valid().Lines),
                new SubmitOrderResult(3, "reference0000000000003", "Pending", 12m, Now));

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.OrderNumber);
            Assert.Empty(store.Added);
            Assert.Equal(0, allocator.Calls);
        }

        [Fact]
        public async Task ARepeatedKeyWithADifferentPayloadIsRefused()
        {
            store.Existing = new StoredOrder("0".PadLeft(64, '0'), new SubmitOrderResult(3, "ref3", "Pending", 12m, Now));

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(IdempotencyErrors.KeyReusedCode, result.Error!.Code);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task TheIdempotencyLookupIsScopedToTheQrBranch()
        {
            await CreateHandler().Handle(Valid(), default);

            Assert.Equal((BranchId, Key), Assert.Single(store.Lookups));
        }

        [Fact]
        public async Task AChangedPriceIsRefusedBeforeAnyOrderIsAllocated()
        {
            store.Catalog = Catalog(unitPrice: 11m);

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.PriceChangedCode, result.Error!.Code);
            Assert.Equal(0, allocator.Calls);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task AnUnavailableProductIsRefused()
        {
            store.Catalog = Catalog(status: ProductStatus.Inactive);

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.Equal(OrdersErrors.ProductUnavailableCode, result.Error!.Code);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task AnUnresolvableQrFailsWithTheUniformNotFoundAndNeverReadsTheCatalog()
        {
            sender.Response = Result.Failure<GuestQrContext>(GuestQrErrors.NotFound());

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(GuestQrErrors.NotFoundCode, result.Error!.Code);
            Assert.Empty(store.Catalogs);
            Assert.Empty(store.Added);
        }

        [Fact]
        public async Task AGuestSubmissionLeavesThePlacingUserEmptyAndACaptainNamesTheStaffUser()
        {
            await CreateHandler().Handle(Valid(), default);
            Assert.Null(Assert.Single(store.Added).PlacedByUserId);

            store.Added.Clear();
            var staffId = Guid.NewGuid();
            await CreateHandler().Handle(
                Valid() with { Source = OrderSource.Captain, ActingStaffUserId = staffId }, default);

            var captainOrder = Assert.Single(store.Added);
            Assert.Equal(OrderSource.Captain, captainOrder.Source);
            Assert.Equal(staffId, captainOrder.PlacedByUserId);
        }

        [Fact]
        public async Task ANumberClashSurfacesAsABusinessConflict()
        {
            store.AddError = OrdersErrors.OrderNumberConflict();

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.OrderNumberConflictCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
        }

        [Fact]
        public async Task AReferenceClashIsRetriedOnceWithAFreshReference()
        {
            store.AddErrors.Enqueue(OrdersErrors.ReferenceConflict());

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, store.Added.Count);
            Assert.Equal(2, references.Calls);
            Assert.NotEqual(store.Added[0].PublicReference, store.Added[1].PublicReference);
        }

        [Fact]
        public async Task ARepeatedReferenceClashIsReportedAsAConflict()
        {
            store.AddErrors.Enqueue(OrdersErrors.ReferenceConflict());
            store.AddErrors.Enqueue(OrdersErrors.ReferenceConflict());

            var result = await CreateHandler().Handle(Valid(), default);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.ReferenceConflictCode, result.Error!.Code);
        }

        [Fact]
        public void TheCommandIsSensitiveSoNothingPersonalReachesTheLogs()
        {
            Assert.IsAssignableFrom<ISensitiveRequest>(Valid());
        }

        [Fact]
        public async Task TheRequestHashIsStoredWithTheOrder()
        {
            await CreateHandler().Handle(Valid(), default);

            Assert.Equal(
                OrderRequestHash.Compute(TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", Valid().Lines),
                Assert.Single(store.Added).RequestHash);
        }

        [Fact]
        public async Task ACaptainOrderIsAcceptedImmediatelyWithTheCaptainAsResponder()
        {
            var staffId = Guid.NewGuid();

            var result = await CreateHandler().Handle(
                Valid() with { Source = OrderSource.Captain, ActingStaffUserId = staffId }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatus.Accepted.ToString(), result.Value.Status);
            var order = Assert.Single(store.Added);
            Assert.Equal(staffId, order.RespondedByUserId);
            Assert.Equal(Now, order.RespondedAt);
            Assert.Throws<InvalidOperationException>(() => order.Expire(Now.AddDays(1)));
        }

        [Fact]
        public async Task ACaptainOrderIsAuditedWithOnlyTheOrderNumber()
        {
            var staffId = Guid.NewGuid();

            await CreateHandler().Handle(
                Valid() with { Source = OrderSource.Captain, ActingStaffUserId = staffId }, default);

            var entry = Assert.Single(audit.Entries);
            Assert.Equal("orders.assisted_created.succeeded", entry.Action);
            Assert.Equal(staffId, entry.ActorUserId);
            var change = Assert.Single(entry.Changes!.Fields);
            Assert.Equal(nameof(Order.OrderNumber), change.Name);
            Assert.Equal("7", change.After);
        }

        [Fact]
        public async Task AGuestOrderWritesNoAuditEntry()
        {
            await CreateHandler().Handle(Valid(), default);

            Assert.Empty(audit.Entries);
        }

        private SubmitOrderCommandHandler CreateHandler() =>
            new(sender, store, allocator, references, new OrdersOptions(), new FixedClock(), audit);

        private static SubmitOrderCommand Valid() => new()
        {
            IdempotencyKey = Key,
            Code = Code,
            ServiceType = OrderServiceType.DineIn,
            GuestDisplayName = "Sara",
            GuestDeviceId = "device-1234567890_ab",
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

        private sealed class FakeAllocator(int number) : IOrderNumberAllocator
        {
            public int Calls { get; private set; }

            public Task<OrderNumberAllocation> AllocateAsync(
                Guid branchId, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(new OrderNumberAllocation(number, new DateOnly(2026, 11, 22)));
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

        private sealed class FakeOrderStore : IOrderStore
        {
            public OrderCatalog Catalog { get; set; } = new([]);

            public List<Order> Added { get; } = [];

            public List<(Guid BranchId, IReadOnlyCollection<Guid> ProductIds)> Catalogs { get; } = [];

            public List<(Guid BranchId, string Key)> Lookups { get; } = [];

            public Queue<Error> AddErrors { get; } = new();

            public Error? AddError { get; set; }

            public StoredOrder? Existing { get; set; }

            public Task<OrderCatalog> LoadCatalogAsync(
                Guid branchId, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
            {
                Catalogs.Add((branchId, productIds));
                return Task.FromResult(Catalog);
            }

            public Task<Result<SubmitOrderResult>> AddAsync(Order order, CancellationToken cancellationToken)
            {
                Added.Add(order);
                var error = AddErrors.Count > 0 ? AddErrors.Dequeue() : AddError;
                return Task.FromResult(error is null
                    ? Result.Success(new SubmitOrderResult(
                        order.OrderNumber,
                        order.PublicReference,
                        order.Status.ToString(),
                        order.TotalAmount,
                        order.ResponseDeadline))
                    : Result.Failure<SubmitOrderResult>(error));
            }

            public Task<StoredOrder?> FindByIdempotencyKeyAsync(
                Guid branchId, string idempotencyKey, CancellationToken cancellationToken)
            {
                Lookups.Add((branchId, idempotencyKey));
                return Task.FromResult(Existing);
            }
        }
    }
}
