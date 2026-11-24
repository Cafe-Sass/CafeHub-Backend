using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Billing
{
    public sealed class CashPaymentTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid TableOne = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid TableTwo = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T12:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public async Task Confirm_RecordsTheAmountActorAndTime()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder("Latte", 10.00m), AcceptedOrder("Tea", 4.50m));
            var handler = CreateHandler(store);

            var result = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                Amount = 10.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(10.00m, result.Value!.Amount);
            Assert.Equal("Cash", result.Value.Method);
            Assert.Equal("Confirmed", result.Value.Status);
            Assert.Equal(Now, result.Value.ConfirmedAt);
            Assert.Equal(10.00m, result.Value.BillPaidAmount);
            Assert.Equal(4.50m, result.Value.BillRemainingAmount);
            var payment = Assert.Single(store.Payments(store.BillId));
            Assert.Equal(UserId, payment.ConfirmedByUserId);
            Assert.Equal(Now, payment.ConfirmedAt);
        }

        [Fact]
        public async Task Confirm_AnAmountAboveTheBillRemainingIsRefused()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder("Latte", 10.00m), AcceptedOrder("Tea", 4.50m));
            store.SeedConfirmedPayment(store.BillId, 10.00m, IdempotencyKey: "cash-key-0001");
            var handler = CreateHandler(store);

            var result = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                Amount = 5.00m,
                IdempotencyKey = "cash-key-0002",
            }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(BillingErrors.AmountAboveRemainingCode, result.Error!.Code);
            Assert.Single(store.Payments(store.BillId));
        }

        [Fact]
        public async Task Confirm_AnAmountAboveTheLinkedOrderRemainingIsRefused()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder("Latte", 10.00m), AcceptedOrder("Tea", 4.50m));
            var tea = store.Orders(store.BillId).Single(order => order.TotalAmount == 4.50m);
            var handler = CreateHandler(store);

            var refused = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                OrderId = tea.Id,
                Amount = 5.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            Assert.False(refused.IsSuccess);
            Assert.Equal(BillingErrors.AmountAboveRemainingCode, refused.Error!.Code);

            var settled = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                OrderId = tea.Id,
                Amount = 4.50m,
                IdempotencyKey = "cash-key-0002",
            }, default);

            Assert.True(settled.IsSuccess, settled.Error?.Code);
            Assert.Equal(0m, Payment.ResolveOrderRemainingAmount(tea, store.Payments(store.BillId)));
        }

        [Fact]
        public void Confirm_ZeroNegativeAndOverPreciseAmountsAreRefusedByTheDomain()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Payment.ConfirmCash(Guid.CreateVersion7(), Guid.CreateVersion7(), null, 0m, UserId, "key", Now, UserId));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Payment.ConfirmCash(Guid.CreateVersion7(), Guid.CreateVersion7(), null, -5.00m, UserId, "key", Now, UserId));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Payment.ConfirmCash(Guid.CreateVersion7(), Guid.CreateVersion7(), null, 10.555m, UserId, "key", Now, UserId));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Payment.ConfirmCash(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Payment.MaximumAmount + 0.01m, UserId, "key", Now, UserId));
        }

        [Fact]
        public async Task Confirm_ARepeatWithTheSameKeyReturnsTheSamePaymentAndTheTotalsChangeOnce()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder("Latte", 10.00m), AcceptedOrder("Tea", 4.50m));
            var handler = CreateHandler(store);
            var first = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                Amount = 10.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            var repeat = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                Amount = 10.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            Assert.True(first.IsSuccess, first.Error?.Code);
            Assert.True(repeat.IsSuccess, repeat.Error?.Code);
            Assert.Equal(first.Value!.PaymentId, repeat.Value!.PaymentId);
            Assert.Equal(10.00m, repeat.Value.BillPaidAmount);
            Assert.Equal(4.50m, repeat.Value.BillRemainingAmount);
            Assert.Single(store.Payments(store.BillId));
        }

        [Fact]
        public async Task Confirm_APaymentLinkedToAnotherBillsOrderIsRefused()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder("Latte", 10.00m), AcceptedOrder("Tea", 4.50m));
            var foreignOrder = store.SeedOpenBillWithAcceptedOrders(TableTwo, AcceptedOrder("Cake", 6.00m));
            var handler = CreateHandler(store);

            var result = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                OrderId = foreignOrder.Id,
                Amount = 6.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(BillingErrors.OrderNotOnBillCode, result.Error!.Code);
            Assert.Empty(store.Payments(store.BillId));
        }

        [Fact]
        public async Task Confirm_ANamedSettlementUpdatesThatOrdersRemaining()
        {
            var store = new FakePaymentStore();
            var order = AcceptedOrder("Latte", 10.00m);
            store.SeedOpenBillWithAcceptedOrders(order, AcceptedOrder("Tea", 4.50m));
            var handler = CreateHandler(store);

            var result = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                OrderId = order.Id,
                Amount = 4.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(order.Id, result.Value!.OrderId);
            Assert.Equal(4.00m, result.Value.BillPaidAmount);
            Assert.Equal(10.50m, result.Value.BillRemainingAmount);
            Assert.Equal(6.00m, Payment.ResolveOrderRemainingAmount(order, store.Payments(store.BillId)));
        }

        [Fact]
        public async Task Confirm_NeverChangesTheOrderStatus()
        {
            var store = new FakePaymentStore();
            var order = AcceptedOrder("Latte", 10.00m);
            store.SeedOpenBillWithAcceptedOrders(order);
            var respondedAt = order.RespondedAt!.Value;
            var handler = CreateHandler(store);

            var result = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                OrderId = order.Id,
                Amount = 10.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(OrderStatus.Accepted, order.Status);
            Assert.Equal(respondedAt, order.RespondedAt);
            Assert.Equal(UserId, order.RespondedByUserId);
        }

        [Fact]
        public async Task Confirm_TheAuditSnapshotHoldsTheMoneyFieldsAndNoGuestDataOrIdempotencyKey()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder("Latte", 10.00m));
            var handler = CreateHandler(store);

            await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                Amount = 10.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            var payment = Assert.Single(store.Payments(store.BillId));
            var snapshot = TenantAuditSnapshotSerializer.Create(
                "payments",
                TenantAuditChangeSets.PaymentConfirmCash(payment));

            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("\"Amount\":\"10.00\"", afterState, StringComparison.Ordinal);
            Assert.Contains("\"Method\":\"Cash\"", afterState, StringComparison.Ordinal);
            Assert.Contains("\"Status\":\"Confirmed\"", afterState, StringComparison.Ordinal);
            Assert.Contains(payment.BillId.ToString(), afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("device", afterState, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sara", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("cash-key-0001", afterState, StringComparison.Ordinal);
        }

        [Fact]
        public void Hybrid_IsDerivedOnlyWhenTwoConfirmedMethodsExist()
        {
            var cash = Payment.ConfirmCash(Guid.CreateVersion7(), Guid.CreateVersion7(), null, 5.00m, UserId, "k1", Now, UserId);
            var online = OnlinePayment(PaymentStatus.Confirmed);
            var pendingOnline = OnlinePayment(PaymentStatus.PendingReview);

            Assert.Equal(PaymentSettlement.Cash, Payment.ResolveSettlementType([cash]));
            Assert.Equal(PaymentSettlement.Online, Payment.ResolveSettlementType([online]));
            Assert.Equal(PaymentSettlement.Hybrid, Payment.ResolveSettlementType([cash, online]));
            Assert.Equal(PaymentSettlement.Cash, Payment.ResolveSettlementType([cash, pendingOnline]));
            Assert.Equal(PaymentSettlement.Cash, Payment.ResolveSettlementType([pendingOnline]));
        }

        [Fact]
        public async Task Confirm_ThePaymentThatClearsTheRemainingAmountClosesTheBill()
        {
            var store = new FakePaymentStore();
            var order = AcceptedOrder("Latte", 10.00m);
            store.SeedOpenBillWithAcceptedOrders(order);
            var bills = new FakeBillStore();
            var receipts = new FakeReceiptIssuer();
            var handler = CreateHandler(store, bills, receipts);

            var result = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                Amount = 10.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(0m, result.Value!.BillRemainingAmount);
            var closed = Assert.Single(bills.Closed);
            Assert.Equal(BillStatus.Closed, closed.Status);
            Assert.Equal(UserId, closed.ClosedByUserId);
            Assert.Null(closed.CloseReason);
            Assert.Equal(0m, Assert.Single(bills.ClosedRemaining));
            Assert.Single(receipts.Issued);
        }

        [Fact]
        public async Task Confirm_APendingPaymentKeepsTheBillOpen()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder("Latte", 10.00m));
            store.SeedConfirmedPayment(store.BillId, 6.00m, IdempotencyKey: "cash-key-0001");
            store.SeedPendingPayment(store.BillId, 4.00m, IdempotencyKey: "online-key-0001");
            var bills = new FakeBillStore();
            var handler = CreateHandler(store, bills);

            var result = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                Amount = 4.00m,
                IdempotencyKey = "cash-key-0002",
            }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(0m, result.Value!.BillRemainingAmount);
            Assert.Empty(bills.Closed);
        }

        [Fact]
        public async Task Confirm_AClosedBillRefusesCash()
        {
            var store = new FakePaymentStore();
            var order = AcceptedOrder("Latte", 10.00m);
            store.SeedOpenBillWithAcceptedOrders(order);
            var settlement = await store.FindSettlementAsync(null, store.BillId, default);
            settlement!.Bill.Close("Staff shift ended", UserId, Now);
            var handler = CreateHandler(store);

            var result = await handler.Handle(new ConfirmCashPaymentCommand
            {
                BillId = store.BillId,
                Amount = 10.00m,
                IdempotencyKey = "cash-key-0001",
            }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(BillingErrors.BillClosedCode, result.Error!.Code);
            Assert.Empty(store.Payments(store.BillId));
        }

        private static ConfirmCashPaymentCommandHandler CreateHandler(
            FakePaymentStore store,
            FakeBillStore? bills = null,
            FakeReceiptIssuer? receipts = null) =>
            new(store, bills ?? new FakeBillStore(), receipts ?? new FakeReceiptIssuer(), StaffUser(), new FixedClock(Now));

        private static FakeTenantUser StaffUser() =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = BranchId });

        private static Payment OnlinePayment(PaymentStatus status)
        {
            var payment = Payment.ConfirmCash(
                Guid.CreateVersion7(), Guid.CreateVersion7(), null, 5.00m, UserId, "k2", Now, UserId);
            typeof(Payment).GetProperty(nameof(Payment.Method))!.SetValue(payment, PaymentMethod.Online);
            typeof(Payment).GetProperty(nameof(Payment.Status))!.SetValue(payment, status);
            return payment;
        }

        private static Order AcceptedOrder(string productName, decimal lineTotal) =>
            Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                TableOne,
                1,
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
                [OrderItem.Create(
                    Guid.CreateVersion7(),
                    1,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    productName,
                    "Regular",
                    lineTotal,
                    1,
                    [],
                    Now.AddMinutes(-5),
                    null)],
                null);

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

        private sealed class FakeReceiptIssuer : IReceiptIssuer
        {
            public List<Bill> Issued { get; } = [];

            public Task<Result> IssueAsync(
                Bill bill,
                IReadOnlyList<Order> acceptedOrders,
                IReadOnlyList<Payment> payments,
                Guid actorId,
                DateTimeOffset now,
                CancellationToken cancellationToken)
            {
                Issued.Add(bill);
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeBillStore : IBillStore
        {
            public List<Bill> Closed { get; } = [];

            public List<decimal> ClosedRemaining { get; } = [];

            public Task<Result<Bill>> ResolveOpenBillAsync(
                Guid branchId,
                Guid? tableId,
                DateTimeOffset now,
                Guid? actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The payment flow must not open a bill.");

            public Task<BillSettlement?> FindForCloseAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The payment flow must not read a bill for closing.");

            public Task<Result> SaveCloseAsync(
                Bill bill,
                decimal remainingAmount,
                Guid actorId,
                CancellationToken cancellationToken)
            {
                Closed.Add(bill);
                ClosedRemaining.Add(remainingAmount);
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakePaymentStore : IPaymentStore
        {
            private readonly Dictionary<Guid, Bill> bills = [];
            private readonly Dictionary<Guid, List<Order>> orders = [];
            private readonly Dictionary<Guid, List<Payment>> payments = [];

            public Guid BillId { get; private set; }

            public IReadOnlyList<Payment> Payments(Guid billId) => payments.GetValueOrDefault(billId, []);

            public IReadOnlyList<Order> Orders(Guid billId) => orders.GetValueOrDefault(billId, []);

            public Order SeedOpenBillWithAcceptedOrders(params Order[] billOrders) =>
                SeedOpenBillWithAcceptedOrders(TableOne, billOrders);

            public Order SeedOpenBillWithAcceptedOrders(Guid tableId, params Order[] billOrders)
            {
                var bill = Bill.Create(Guid.CreateVersion7(), BranchId, tableId, Reference, Now, null);
                bills[bill.Id] = bill;
                if (BillId == Guid.Empty)
                {
                    BillId = bill.Id;
                }

                var billOrderList = new List<Order>();
                foreach (var order in billOrders)
                {
                    order.Accept(UserId, Now);
                    order.AttachToBill(bill.Id);
                    billOrderList.Add(order);
                }

                orders[bill.Id] = billOrderList;
                return billOrders[0];
            }

            public void SeedConfirmedPayment(Guid billId, decimal amount, string IdempotencyKey)
            {
                var payment = Payment.ConfirmCash(
                    Guid.CreateVersion7(), billId, null, amount, UserId, IdempotencyKey, Now, UserId);
                if (!payments.TryGetValue(billId, out var billPayments))
                {
                    billPayments = [];
                    payments[billId] = billPayments;
                }

                billPayments.Add(payment);
            }

            public void SeedPendingPayment(Guid billId, decimal amount, string IdempotencyKey)
            {
                var payment = Payment.ConfirmCash(
                    Guid.CreateVersion7(), billId, null, amount, UserId, IdempotencyKey, Now, UserId);
                typeof(Payment).GetProperty(nameof(Payment.Status))!.SetValue(payment, PaymentStatus.PendingReview);
                if (!payments.TryGetValue(billId, out var billPayments))
                {
                    billPayments = [];
                    payments[billId] = billPayments;
                }

                billPayments.Add(payment);
            }

            public Task<BillSettlement?> FindSettlementAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken)
            {
                if (!bills.TryGetValue(billId, out var bill)
                    || (branchId is { } branch && bill.BranchId != branch))
                {
                    return Task.FromResult<BillSettlement?>(null);
                }

                return Task.FromResult<BillSettlement?>(new BillSettlement(
                    bill,
                    orders.GetValueOrDefault(billId, []).ToList(),
                    payments.GetValueOrDefault(billId, []).ToList()));
            }

            public Task<BillSettlement?> FindForProofAsync(
                string billReference,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The cash flow must not look up a bill for a proof.");

            public Task<Result<Payment>> AddProofAsync(Payment payment, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The cash flow must not submit a proof.");

            public Task<PaymentReview?> FindReviewAsync(
                Guid? branchId,
                Guid paymentId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The cash flow must not review a payment.");

            public Task<Result<Payment>> SaveReviewAsync(
                Payment payment,
                string? rejectionReason,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The cash flow must not review a payment.");

            public Task<Result<Payment>> AddAsync(Payment payment, CancellationToken cancellationToken)
            {
                var existing = payments.GetValueOrDefault(payment.BillId, [])
                    .FirstOrDefault(stored => stored.IdempotencyKey == payment.IdempotencyKey);
                if (existing is not null)
                {
                    return Task.FromResult(Result.Success(existing));
                }

                if (!payments.TryGetValue(payment.BillId, out var billPayments))
                {
                    billPayments = [];
                    payments[payment.BillId] = billPayments;
                }

                billPayments.Add(payment);
                return Task.FromResult(Result.Success(payment));
            }
        }
    }
}
