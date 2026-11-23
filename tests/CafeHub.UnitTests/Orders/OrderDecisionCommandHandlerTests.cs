using System.Text.Json;
using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrderDecisionCommandHandlerTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid StationOne = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid StationTwo = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-22T10:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public async Task Accept_APendingOrderTransitionsToAcceptedAndStoresTheDeciderAndTime()
        {
            var order = PendingOrder();
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateAcceptHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now);

            var result = await handler.Handle(new AcceptOrderCommand(order.Id), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatus.Accepted, order.Status);
            Assert.Equal(UserId, order.RespondedByUserId);
            Assert.Equal(Now, order.RespondedAt);
            Assert.Equal(Now, order.UpdatedAt);
            Assert.Equal(7, result.Value!.OrderNumber);
            Assert.Equal("Accepted", result.Value.Status);
        }

        [Fact]
        public async Task Accept_StaffAreFixedToTheirOwnBranch()
        {
            var store = new FakeOrderDecisionStore(PendingOrder());
            var handler = CreateAcceptHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now);

            await handler.Handle(new AcceptOrderCommand(store.Target!.Id), default);

            Assert.Equal(BranchId, store.LastBranchLookup);
        }

        [Fact]
        public async Task Accept_TheOwnerMayDecideAnOrderOfAnyBranch()
        {
            var store = new FakeOrderDecisionStore(PendingOrder());
            var handler = CreateAcceptHandler(store, OwnerUser(), new RecordingAuditWriter(), Now);

            var result = await handler.Handle(new AcceptOrderCommand(store.Target!.Id), default);

            Assert.True(result.IsSuccess);
            Assert.Null(store.LastBranchLookup);
        }

        [Fact]
        public async Task Accept_AForeignOrderIsIndistinguishableFromAMissingOne()
        {
            var store = new FakeOrderDecisionStore(null);
            var handler = CreateAcceptHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now);

            var result = await handler.Handle(new AcceptOrderCommand(Guid.CreateVersion7()), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(BranchId, store.LastBranchLookup);
        }

        [Fact]
        public async Task Accept_StationScopedStaffWhoSeeNoItemsGetTheUniformNotFound()
        {
            var order = PendingOrder(Item(StationTwo, 1));
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateAcceptHandler(
                store, StaffUser(BranchId), new RecordingAuditWriter(), Now, new FakeStationScope([StationOne]));

            var result = await handler.Handle(new AcceptOrderCommand(order.Id), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
        }

        [Fact]
        public async Task Accept_StationScopedStaffMayDecideOrdersWithTheirStationItemsOrUnroutedItems()
        {
            var order = PendingOrder(Item(StationOne, 1), Item(null, 2));
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateAcceptHandler(
                store, StaffUser(BranchId), new RecordingAuditWriter(), Now, new FakeStationScope([StationOne]));

            var result = await handler.Handle(new AcceptOrderCommand(order.Id), default);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task Accept_ANonPendingOrderIsAConflictThatNamesTheCurrentStatus()
        {
            var order = PendingOrder();
            order.Accept(UserId, Now);
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateAcceptHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now);

            var result = await handler.Handle(new AcceptOrderCommand(order.Id), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.StatusConflictCode, result.Error!.Code);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error);
            Assert.Equal("Accepted", details.Details[OrdersErrors.CurrentStatusExtension]);
        }

        [Fact]
        public async Task Accept_AStaleOrderIsExpiredFirstAndTheDecisionIsRefusedAsConflict()
        {
            var order = PendingOrder(Now.AddSeconds(-30));
            var sender = ExpireOnSend(order, Now);
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateAcceptHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now, sender: sender);

            var result = await handler.Handle(new AcceptOrderCommand(order.Id), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.StatusConflictCode, result.Error!.Code);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error);
            Assert.Equal("Expired", details.Details[OrdersErrors.CurrentStatusExtension]);
            Assert.Equal(OrderStatus.Expired, order.Status);
            var expire = Assert.IsType<ExpireStaleOrdersCommand>(Assert.Single(sender.Sent));
            Assert.Null(expire.BranchId);
            Assert.Equal(order.Id, expire.OrderId);
        }

        [Fact]
        public async Task Accept_TheResponseDeadlineBoundaryIsInclusive()
        {
            var order = PendingOrder(Now);
            var sender = ExpireOnSend(order, Now);
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateAcceptHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now, sender: sender);

            var result = await handler.Handle(new AcceptOrderCommand(order.Id), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.StatusConflictCode, result.Error!.Code);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error);
            Assert.Equal("Expired", details.Details[OrdersErrors.CurrentStatusExtension]);
            Assert.Equal(OrderStatus.Expired, order.Status);
        }

        [Fact]
        public async Task Accept_ALostConcurrencyRaceReturnsAConflictAndWritesNoAudit()
        {
            var order = PendingOrder();
            var audit = new RecordingAuditWriter();
            var store = new FakeOrderDecisionStore(order)
            {
                SaveFailure = OrdersErrors.StatusConflict(OrderStatus.Accepted),
            };
            var handler = CreateAcceptHandler(store, StaffUser(BranchId), audit, Now);

            var result = await handler.Handle(new AcceptOrderCommand(order.Id), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.StatusConflictCode, result.Error!.Code);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Reject_APendingOrderTransitionsToRejectedAndStoresTheTrimmedReason()
        {
            var order = PendingOrder();
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateRejectHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now);

            var result = await handler.Handle(new RejectOrderCommand(order.Id, "  Out of oat milk  "), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(OrderStatus.Rejected, order.Status);
            Assert.Equal(UserId, order.RespondedByUserId);
            Assert.Equal(Now, order.RespondedAt);
            Assert.Equal("Out of oat milk", order.RejectionReason);
            Assert.Equal("Rejected", result.Value!.Status);
        }

        [Fact]
        public async Task Reject_AForeignOrderIsIndistinguishableFromAMissingOne()
        {
            var store = new FakeOrderDecisionStore(null);
            var handler = CreateRejectHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now);

            var result = await handler.Handle(new RejectOrderCommand(Guid.CreateVersion7(), "Unknown order"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(BranchId, store.LastBranchLookup);
        }

        [Fact]
        public async Task Reject_StationScopedStaffWhoSeeNoItemsGetTheUniformNotFound()
        {
            var order = PendingOrder(Item(StationTwo, 1));
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateRejectHandler(
                store, StaffUser(BranchId), new RecordingAuditWriter(), Now, new FakeStationScope([StationOne]));

            var result = await handler.Handle(new RejectOrderCommand(order.Id, "Out of oat milk"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
        }

        [Fact]
        public async Task Reject_AStaleOrderIsExpiredFirstAndTheDecisionIsRefusedAsConflict()
        {
            var order = PendingOrder(Now.AddSeconds(-30));
            var sender = ExpireOnSend(order, Now);
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateRejectHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now, sender: sender);

            var result = await handler.Handle(new RejectOrderCommand(order.Id, "Out of oat milk"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.StatusConflictCode, result.Error!.Code);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error);
            Assert.Equal("Expired", details.Details[OrdersErrors.CurrentStatusExtension]);
            Assert.Equal(OrderStatus.Expired, order.Status);
            var expire = Assert.IsType<ExpireStaleOrdersCommand>(Assert.Single(sender.Sent));
            Assert.Null(expire.BranchId);
            Assert.Equal(order.Id, expire.OrderId);
        }

        [Theory]
        [InlineData(OrderStatus.Accepted)]
        [InlineData(OrderStatus.Rejected)]
        public async Task Reject_ADecidedOrderIsAConflictThatNamesTheCurrentStatus(OrderStatus decided)
        {
            var order = PendingOrder();
            if (decided == OrderStatus.Accepted)
            {
                order.Accept(UserId, Now);
            }
            else
            {
                order.Reject(UserId, "Already decided", Now);
            }

            var store = new FakeOrderDecisionStore(order);
            var handler = CreateRejectHandler(store, StaffUser(BranchId), new RecordingAuditWriter(), Now);

            var result = await handler.Handle(new RejectOrderCommand(order.Id, "Out of oat milk"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.StatusConflictCode, result.Error!.Code);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error);
            Assert.Equal(decided.ToString(), details.Details[OrdersErrors.CurrentStatusExtension]);
        }

        [Fact]
        public async Task Reject_ALostConcurrencyRaceReturnsAConflictAndWritesNoAudit()
        {
            var order = PendingOrder();
            var audit = new RecordingAuditWriter();
            var store = new FakeOrderDecisionStore(order)
            {
                SaveFailure = OrdersErrors.StatusConflict(OrderStatus.Rejected),
            };
            var handler = CreateRejectHandler(store, StaffUser(BranchId), audit, Now);

            var result = await handler.Handle(new RejectOrderCommand(order.Id, "Out of oat milk"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.StatusConflictCode, result.Error!.Code);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task TheAcceptAuditHoldsTheStatusChangeAndNeverGuestPersonalData()
        {
            var order = PendingOrder();
            var audit = new RecordingAuditWriter();
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateAcceptHandler(store, StaffUser(BranchId), audit, Now);

            await handler.Handle(new AcceptOrderCommand(order.Id), default);

            var entry = Assert.Single(audit.Entries);
            Assert.Equal("orders.accepted.succeeded", entry.Action);
            Assert.Equal("orders", entry.TargetType);
            Assert.Equal(order.Id, entry.TargetId);
            Assert.Equal(UserId, entry.ActorUserId);
            var change = Assert.Single(entry.Changes!.Fields);
            Assert.Equal(nameof(Order.Status), change.Name);
            Assert.Equal(OrderStatus.Pending.ToString(), change.Before);
            Assert.Equal(OrderStatus.Accepted.ToString(), change.After);
            var json = JsonSerializer.Serialize(entry);
            Assert.DoesNotContain("Sara", json, StringComparison.Ordinal);
            Assert.DoesNotContain("device", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task TheRejectAuditHoldsTheStatusChangeAndTheReason()
        {
            var order = PendingOrder();
            var audit = new RecordingAuditWriter();
            var store = new FakeOrderDecisionStore(order);
            var handler = CreateRejectHandler(store, StaffUser(BranchId), audit, Now);

            await handler.Handle(new RejectOrderCommand(order.Id, "  Out of oat milk  "), default);

            var entry = Assert.Single(audit.Entries);
            Assert.Equal("orders.rejected.succeeded", entry.Action);
            Assert.Equal("orders", entry.TargetType);
            Assert.Equal(order.Id, entry.TargetId);
            Assert.Equal(UserId, entry.ActorUserId);
            Assert.Equal(2, entry.Changes!.Fields.Count);
            var status = entry.Changes.Fields.Single(field => field.Name == nameof(Order.Status));
            Assert.Equal(OrderStatus.Pending.ToString(), status.Before);
            Assert.Equal(OrderStatus.Rejected.ToString(), status.After);
            var reason = entry.Changes.Fields.Single(field => field.Name == nameof(Order.RejectionReason));
            Assert.Null(reason.Before);
            Assert.Equal("Out of oat milk", reason.After);
            var json = JsonSerializer.Serialize(entry);
            Assert.DoesNotContain("Sara", json, StringComparison.Ordinal);
            Assert.DoesNotContain("device", json, StringComparison.OrdinalIgnoreCase);
        }

        private static AcceptOrderCommandHandler CreateAcceptHandler(
            FakeOrderDecisionStore store,
            FakeTenantUser user,
            RecordingAuditWriter audit,
            DateTimeOffset now,
            FakeStationScope? stations = null,
            FakeSender? sender = null,
            IBillStore? bills = null) =>
            new(sender ?? new FakeSender(), store, bills ?? new FakeBillStore(), user, stations ?? new FakeStationScope(null), new FixedClock(now), audit);

        private static RejectOrderCommandHandler CreateRejectHandler(
            FakeOrderDecisionStore store,
            FakeTenantUser user,
            RecordingAuditWriter audit,
            DateTimeOffset now,
            FakeStationScope? stations = null,
            FakeSender? sender = null) =>
            new(sender ?? new FakeSender(), store, user, stations ?? new FakeStationScope(null), new FixedClock(now), audit);

        private static FakeSender ExpireOnSend(Order order, DateTimeOffset now) =>
            new(request =>
            {
                if (request is ExpireStaleOrdersCommand expire
                    && expire.OrderId == order.Id
                    && order.Status == OrderStatus.Pending
                    && order.ResponseDeadline <= now)
                {
                    order.Expire(now);
                }

                return null;
            });

        private static FakeTenantUser StaffUser(Guid branchId) =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = branchId });

        private static FakeTenantUser OwnerUser() =>
            new(Guid.CreateVersion7(), new TenantAccountAccess(false, null, false));

        private static Order PendingOrder(params OrderItem[] items) =>
            PendingOrder(Now.AddMinutes(5), items);

        private static Order PendingOrder(DateTimeOffset deadline, params OrderItem[] items) =>
            Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                Guid.Parse("66666666-6666-6666-6666-666666666666"),
                7,
                new DateOnly(2026, 11, 22),
                OrderServiceType.DineIn,
                OrderSource.Guest,
                "Sara",
                "device-identifier-0001",
                null,
                Reference,
                "idempotency-key",
                null,
                Now.AddMinutes(-5),
                deadline,
                items.Length == 0 ? new[] { Item(StationOne, 1) } : items,
                null);

        private static OrderItem Item(Guid? stationId, int lineNumber) =>
            OrderItem.Create(
                Guid.CreateVersion7(),
                lineNumber,
                Guid.NewGuid(),
                Guid.NewGuid(),
                stationId,
                "Latte",
                "Regular",
                4.50m,
                1,
                [],
                Now.AddMinutes(-5),
                null);

        private sealed class FakeOrderDecisionStore(Order? target) : IOrderDecisionStore
        {
            public Order? Target { get; } = target;

            public Guid? LastBranchLookup { get; private set; }

            public Error? SaveFailure { get; init; }

            public Task<Order?> FindDecisionTargetAsync(Guid? branchId, Guid orderId, CancellationToken cancellationToken)
            {
                LastBranchLookup = branchId;
                return Task.FromResult(Target);
            }

            public Task<Result<OrderStatus>> SaveDecisionAsync(Order order, CancellationToken cancellationToken)
            {
                return SaveFailure is not null
                    ? Task.FromResult(Result.Failure<OrderStatus>(SaveFailure))
                    : Task.FromResult(Result.Success(order.Status));
            }
        }

        private sealed class FakeBillStore : IBillStore
        {
            private readonly Dictionary<Guid, Bill> openBills = [];

            public Task<Result<Bill>> ResolveOpenBillAsync(
                Guid branchId,
                Guid tableId,
                DateTimeOffset now,
                Guid? actorId,
                CancellationToken cancellationToken)
            {
                if (!openBills.TryGetValue(tableId, out var bill))
                {
                    bill = Bill.Create(Guid.CreateVersion7(), branchId, tableId, "Abcdefghijklmnopqrstuv", now, actorId);
                    openBills[tableId] = bill;
                }

                return Task.FromResult(Result.Success(bill));
            }

            public Task<BillSettlement?> FindForCloseAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The decision flow must not read a bill for closing.");

            public Task<Result> SaveCloseAsync(
                Bill bill,
                decimal remainingAmount,
                Guid actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The decision flow must not close a bill.");
        }

        private sealed class FakeStationScope(IReadOnlyList<Guid>? stationIds) : IStationScope
        {
            public Task<IReadOnlyList<Guid>?> GetVisibleStationIdsAsync(Guid userId, CancellationToken cancellationToken) =>
                Task.FromResult(stationIds);
        }

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => BranchId;

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FakeSender(Func<object, object?>? onSend = null) : ISender
        {
            public List<object> Sent { get; } = [];

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                Sent.Add(request);
                var result = onSend?.Invoke(request);
                if (result is not null)
                {
                    return Task.FromResult((TResponse)result);
                }

                var response = request is ExpireStaleOrdersCommand
                    ? (TResponse)(object)Result.Success<IReadOnlyList<Guid>>(Array.Empty<Guid>())
                    : default;
                return Task.FromResult(response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest
            {
                Sent.Add(request);
                return Task.CompletedTask;
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            {
                Sent.Add(request);
                return Task.FromResult(onSend?.Invoke(request));
            }

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The handler must not create streams.");

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The handler must not create streams.");
        }

        private sealed class RecordingAuditWriter : ITenantAuditWriter
        {
            public List<TenantAuditEntry> Entries { get; } = [];

            public Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken)
            {
                Entries.Add(entry);
                return Task.FromResult(Result.Success());
            }
        }
    }
}
