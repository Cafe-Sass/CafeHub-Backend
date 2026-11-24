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
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;
using MediatR;

namespace CafeHub.UnitTests.Billing
{
    public sealed class BillAttachmentTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid TableId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T10:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public async Task Accept_TheFirstAcceptedOrderOpensTheTableBill()
        {
            var order = PendingOrder();
            var bills = new FakeBillStore();
            var audit = new RecordingAuditWriter();
            var handler = CreateAcceptHandler(new FakeDecisionStore(order, bills), bills, StaffUser(), audit);

            var result = await handler.Handle(new AcceptOrderCommand(order.Id), default);

            Assert.True(result.IsSuccess);
            var bill = Assert.Single(bills.Committed);
            Assert.Equal(BillStatus.Open, bill.Status);
            Assert.Equal(BranchId, bill.BranchId);
            Assert.Equal(TableId, bill.TableId);
            Assert.Equal(Now, bill.OpenedAt);
            Assert.Equal(22, bill.PublicReference.Length);
            Assert.Equal(bill.Id, order.BillId);
        }

        [Fact]
        public async Task Accept_ASecondAcceptedOrderForTheTableAttachesToTheSameOpenBill()
        {
            var first = PendingOrder();
            var second = PendingOrder();
            var bills = new FakeBillStore();
            var handler = CreateAcceptHandler(new FakeDecisionStore(first, bills), bills, StaffUser(), new RecordingAuditWriter());

            await handler.Handle(new AcceptOrderCommand(first.Id), default);

            var secondHandler = CreateAcceptHandler(new FakeDecisionStore(second, bills), bills, StaffUser(), new RecordingAuditWriter());
            var result = await secondHandler.Handle(new AcceptOrderCommand(second.Id), default);

            Assert.True(result.IsSuccess);
            var bill = Assert.Single(bills.Committed);
            Assert.Equal(bill.Id, first.BillId);
            Assert.Equal(bill.Id, second.BillId);
        }

        [Fact]
        public async Task Accept_AConcurrentAcceptForTheSameTableEndsWithOneOpenBill()
        {
            var first = PendingOrder();
            var second = PendingOrder();
            var bills = new FakeBillStore(raceMode: true);
            var handler = CreateAcceptHandler(new FakeDecisionStore(first, bills), bills, StaffUser(), new RecordingAuditWriter());

            await handler.Handle(new AcceptOrderCommand(first.Id), default);

            var secondHandler = CreateAcceptHandler(new FakeDecisionStore(second, bills), bills, StaffUser(), new RecordingAuditWriter());
            var result = await secondHandler.Handle(new AcceptOrderCommand(second.Id), default);

            Assert.True(result.IsSuccess);
            var bill = Assert.Single(bills.Committed);
            Assert.Empty(bills.Pending);
            Assert.Equal(bill.Id, first.BillId);
            Assert.Equal(bill.Id, second.BillId);
        }

        [Fact]
        public async Task Accept_TheOpeningAuditHoldsTheBillFieldsAndNeverGuestPersonalData()
        {
            var order = PendingOrder();
            var bills = new FakeBillStore();
            var audit = new RecordingAuditWriter();
            var handler = CreateAcceptHandler(new FakeDecisionStore(order, bills), bills, StaffUser(), audit);

            await handler.Handle(new AcceptOrderCommand(order.Id), default);

            var entry = Assert.Single(audit.Entries, candidate => candidate.TargetType == "bills");
            Assert.Equal("bills.opened.succeeded", entry.Action);
            Assert.Equal(UserId, entry.ActorUserId);
            Assert.Equal(bills.Committed.Single().Id, entry.TargetId);
            var changes = entry.Changes!.Fields;
            Assert.Equal(3, changes.Count);
            Assert.Equal(BillStatus.Open.ToString(), changes.Single(field => field.Name == nameof(Bill.Status)).After);
            Assert.Null(changes.Single(field => field.Name == nameof(Bill.Status)).Before);
            var json = JsonSerializer.Serialize(entry);
            Assert.DoesNotContain("Sara", json, StringComparison.Ordinal);
            Assert.DoesNotContain("device", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Accept_AnAlreadyOpenBillIsReusedWithoutASecondOpeningAudit()
        {
            var first = PendingOrder();
            var second = PendingOrder();
            var bills = new FakeBillStore();
            var firstAudit = new RecordingAuditWriter();
            await CreateAcceptHandler(new FakeDecisionStore(first, bills), bills, StaffUser(), firstAudit)
                .Handle(new AcceptOrderCommand(first.Id), default);

            var secondAudit = new RecordingAuditWriter();
            await CreateAcceptHandler(new FakeDecisionStore(second, bills), bills, StaffUser(), secondAudit)
                .Handle(new AcceptOrderCommand(second.Id), default);

            Assert.DoesNotContain(secondAudit.Entries, entry => entry.TargetType == "bills");
            var entry = Assert.Single(firstAudit.Entries, candidate => candidate.TargetType == "bills");
            Assert.Equal("bills.opened.succeeded", entry.Action);
        }

        [Fact]
        public async Task Reject_ARejectedOrderNeverAttachesToABill()
        {
            var order = PendingOrder();
            var bills = new FakeBillStore();
            var audit = new RecordingAuditWriter();
            var handler = new RejectOrderCommandHandler(
                new FakeSender(), new FakeDecisionStore(order, bills), StaffUser(), new FakeStationScope(null), new FixedClock(Now), audit);

            var result = await handler.Handle(new RejectOrderCommand(order.Id, "Out of oat milk"), default);

            Assert.True(result.IsSuccess);
            Assert.Null(order.BillId);
            Assert.Empty(bills.Committed);
            Assert.DoesNotContain(audit.Entries, entry => entry.TargetType == "bills");
        }

        private static AcceptOrderCommandHandler CreateAcceptHandler(
            FakeDecisionStore store,
            FakeBillStore bills,
            FakeTenantUser user,
            RecordingAuditWriter audit)
        {
            bills.Audit = audit;
            return new(new FakeSender(), store, bills, new FakeWifiVoucherStore(), user, new FakeStationScope(null), new FixedClock(Now), audit);
        }

        private static FakeTenantUser StaffUser() =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = BranchId });

        private static Order PendingOrder(params OrderItem[] items) =>
            Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                TableId,
                7,
                new DateOnly(2026, 11, 23),
                OrderServiceType.DineIn,
                OrderSource.Guest,
                "Sara",
                "device-identifier-0001",
                null,
                Reference,
                "idempotency-key",
                null,
                Now.AddMinutes(-5),
                Now.AddMinutes(5),
                items.Length == 0 ? new[] { Item() } : items,
                null);

        private static OrderItem Item() =>
            OrderItem.Create(
                Guid.CreateVersion7(),
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Latte",
                "Regular",
                4.50m,
                1,
                [],
                Now.AddMinutes(-5),
                null);

        private sealed class FakeDecisionStore(Order? target, FakeBillStore bills) : IOrderDecisionStore
        {
            public Order? Target { get; } = target;

            public Guid? LastBranchLookup { get; private set; }

            public Task<Order?> FindDecisionTargetAsync(Guid? branchId, Guid orderId, CancellationToken cancellationToken)
            {
                LastBranchLookup = branchId;
                return Task.FromResult(Target);
            }

            public Task<Result<OrderStatus>> SaveDecisionAsync(Order order, CancellationToken cancellationToken)
            {
                if (order.BillId is { } attachedId)
                {
                    var pending = bills.Pending.FirstOrDefault(bill => bill.Id == attachedId);
                    if (pending is not null)
                    {
                        var winner = bills.FindOpen(order.TableId);
                        if (winner is not null)
                        {
                            bills.Discard(pending);
                            order.AttachToBill(winner.Id);
                        }
                        else
                        {
                            bills.Commit(pending);
                        }
                    }
                }

                return Task.FromResult(Result.Success(order.Status));
            }
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
                throw new NotSupportedException("The bill flow does not import vouchers.");

            public Task<Result> InsertImportedAsync(
                Guid packageId,
                IReadOnlyList<WifiVoucher> vouchers,
                WifiVoucherImportResult counts,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The bill flow does not import vouchers.");

            public Task<WifiVoucherSummary> GetSummaryAsync(
                Guid packageId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The bill flow does not read voucher counts.");

            public Task<Result> VoidAsync(
                Guid packageId,
                string code,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The bill flow does not void vouchers.");
        }

        private sealed class FakeBillStore(bool raceMode = false) : IBillStore
        {
            private readonly List<Bill> committed = [];
            private readonly List<Bill> pending = [];

            public RecordingAuditWriter? Audit { get; set; }

            public IReadOnlyList<Bill> Committed => committed;

            public IReadOnlyList<Bill> Pending => pending;

            public async Task<Result<Bill>> ResolveOpenBillAsync(
                Guid branchId,
                Guid? tableId,
                DateTimeOffset now,
                Guid? actorId,
                CancellationToken cancellationToken)
            {
                if (!raceMode)
                {
                    var open = committed.FirstOrDefault(
                        bill => bill.TableId == tableId && bill.Status == BillStatus.Open);
                    if (open is not null)
                    {
                        return Result.Success(open);
                    }
                }

                var bill = Bill.Create(Guid.CreateVersion7(), branchId, tableId, Reference, now, actorId);
                pending.Add(bill);
                if (Audit is not null)
                {
                    await Audit.WriteAsync(new TenantAuditEntry
                    {
                        Action = "bills.opened.succeeded",
                        TargetType = "bills",
                        TargetId = bill.Id,
                        ActorUserId = actorId,
                        Changes = TenantAuditChanges.Create(
                        [
                            new TenantAuditChanges.FieldChange(nameof(Bill.Status), null, bill.Status.ToString()),
                            new TenantAuditChanges.FieldChange(nameof(Bill.PublicReference), null, bill.PublicReference),
                            new TenantAuditChanges.FieldChange(nameof(Bill.BranchId), null, bill.BranchId.ToString()),
                        ]),
                    }, cancellationToken);
                }

                return Result.Success(bill);
            }

            public Bill? FindOpen(Guid? tableId) =>
                committed.FirstOrDefault(bill => bill.TableId == tableId && bill.Status == BillStatus.Open);

            public Task<BillSettlement?> FindForCloseAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The accept flow must not read a bill for closing.");

            public Task<Result> SaveCloseAsync(
                Bill bill,
                decimal remainingAmount,
                Guid actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The accept flow must not close a bill.");

            public void Commit(Bill bill)
            {
                pending.Remove(bill);
                committed.Add(bill);
            }

            public void Discard(Bill bill) => pending.Remove(bill);
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
