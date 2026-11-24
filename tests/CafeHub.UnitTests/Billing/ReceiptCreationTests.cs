using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Billing
{
    public sealed class ReceiptCreationTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid TableId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public void A_FullyPaidBillCreatesOneReceiptWithItsLinesAndPayments()
        {
            var bill = Bill.Create(Guid.CreateVersion7(), BranchId, TableId, Reference, Now, null);
            var latte = AcceptedOrder(bill, "Sara", "Latte", "Regular", 2.25m, 2, [Addon("Shot", 0.50m)], 1);
            var tea = AcceptedOrder(bill, "Omar", "Tea", "Large", 5.00m, 1, [], 2);
            var cash = Payment.ConfirmCash(
                Guid.CreateVersion7(), bill.Id, null, 4.00m, UserId, "cash-key", Now.AddMinutes(-2), UserId);
            var online = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(), bill.Id, 6.00m, MediaReference(), null, "online-key", Now.AddMinutes(-3));
            online.ConfirmReview(6.00m, UserId, Now.AddMinutes(-1));

            var receipt = ReceiptComposer.Compose(
                Guid.CreateVersion7(),
                bill,
                [latte, tea],
                [online, cash],
                Receipt.FormatReceiptNumber(new DateOnly(2026, 11, 24), 1),
                Now,
                UserId);

            Assert.Equal("R-20261124-1", receipt.ReceiptNumber);
            Assert.Equal(10.00m, receipt.TotalAmount);
            Assert.Equal(10.00m, receipt.PaidAmount);
            Assert.Equal(0m, receipt.RemainingAmount);
            Assert.Equal(2, receipt.Lines.Count);
            Assert.Equal("Sara", receipt.Lines[0].GuestDisplayName);
            Assert.Equal("Latte", receipt.Lines[0].ProductName);
            Assert.Equal("Regular", receipt.Lines[0].SizeName);
            Assert.Equal("Shot", receipt.Lines[0].Addons);
            Assert.Equal(2, receipt.Lines[0].Quantity);
            Assert.Equal(2.25m, receipt.Lines[0].UnitPrice);
            Assert.Equal(5.00m, receipt.Lines[0].LineTotal);
            Assert.Equal("Omar", receipt.Lines[1].GuestDisplayName);
            Assert.Equal(Receipt.NeutralGuestName, ReceiptLine.NormalizeGuestName("   "));
            Assert.All(receipt.Lines, line => Assert.Equal(receipt.Id, line.ReceiptId));
            Assert.Equal(new[] { PaymentMethod.Cash, PaymentMethod.Online }, receipt.Payments.Select(payment => payment.Method));
            Assert.Equal(new[] { 4.00m, 6.00m }, receipt.Payments.Select(payment => payment.Amount));
            Assert.All(receipt.Payments, payment =>
            {
                Assert.Equal(receipt.Id, payment.ReceiptId);
                Assert.NotEqual(default, payment.ConfirmedAt);
            });
            Assert.Equal(10.00m, receipt.Lines.Sum(line => line.LineTotal));
        }

        [Fact]
        public void TheSnapshotKeepsItsValuesWhenTheOrdersChangeLater()
        {
            var bill = Bill.Create(Guid.CreateVersion7(), BranchId, TableId, Reference, Now, null);
            var order = AcceptedOrder(bill, "Sara", "Latte", "Regular", 2.50m, 2, [Addon("Shot", 0.50m)]);
            var payment = Payment.ConfirmCash(
                Guid.CreateVersion7(), bill.Id, null, 5.50m, UserId, "cash-key", Now, UserId);
            var receipt = ReceiptComposer.Compose(
                Guid.CreateVersion7(), bill, [order], [payment],
                Receipt.FormatReceiptNumber(new DateOnly(2026, 11, 24), 1), Now, UserId);

            typeof(OrderItem).GetProperty(nameof(OrderItem.UnitPrice))!.SetValue(order.Items[0], 99.00m);
            typeof(OrderItem).GetProperty(nameof(OrderItem.LineTotal))!.SetValue(order.Items[0], 198.00m);
            typeof(OrderItem).GetProperty(nameof(OrderItem.ProductName))!.SetValue(order.Items[0], "Changed");
            typeof(Order).GetProperty(nameof(Order.GuestDisplayName))!.SetValue(order, "Other");

            var line = Assert.Single(receipt.Lines);
            Assert.Equal("Latte", line.ProductName);
            Assert.Equal("Sara", line.GuestDisplayName);
            Assert.Equal(2.50m, line.UnitPrice);
            Assert.Equal(5.50m, line.LineTotal);
            Assert.Equal(5.50m, receipt.TotalAmount);
        }

        [Fact]
        public async Task ARepeatCompletionCreatesNoSecondReceiptAndClosesOnce()
        {
            var store = new FakePaymentStore();
            var order = AcceptedOrder(store.Bill, "Sara", "Latte", "Regular", 10.00m, 1, []);
            store.Track(order);
            var bills = new FakeBillStore();
            var receipts = new FakeReceiptIssuer();
            var handler = CashHandler(store, bills, receipts);

            var first = await handler.Handle(
                new ConfirmCashPaymentCommand { BillId = store.Bill.Id, Amount = 10.00m, IdempotencyKey = "cash-key" },
                default);
            var repeat = await handler.Handle(
                new ConfirmCashPaymentCommand { BillId = store.Bill.Id, Amount = 10.00m, IdempotencyKey = "cash-key" },
                default);

            Assert.True(first.IsSuccess, first.Error?.Code);
            Assert.True(repeat.IsSuccess, repeat.Error?.Code);
            Assert.Single(bills.Closed);
            Assert.Single(receipts.Issued);
            Assert.Equal(BillStatus.Closed, store.Bill.Status);
        }

        [Fact]
        public async Task AnUnpaidBillIssuesNoReceipt()
        {
            var store = new FakePaymentStore();
            var order = AcceptedOrder(store.Bill, "Sara", "Latte", "Regular", 10.00m, 1, []);
            store.Track(order);
            var bills = new FakeBillStore();
            var receipts = new FakeReceiptIssuer();

            var result = await CashHandler(store, bills, receipts).Handle(
                new ConfirmCashPaymentCommand { BillId = store.Bill.Id, Amount = 4.00m, IdempotencyKey = "cash-key" },
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Empty(bills.Closed);
            Assert.Empty(receipts.Issued);
            Assert.Equal(BillStatus.Open, store.Bill.Status);
        }

        [Fact]
        public async Task APendingProofPreventsTheReceiptEvenAtZeroRemaining()
        {
            var store = new FakePaymentStore();
            var order = AcceptedOrder(store.Bill, "Sara", "Latte", "Regular", 10.00m, 1, []);
            store.Track(order);
            store.SeedPendingProof(1.00m);
            var bills = new FakeBillStore();
            var receipts = new FakeReceiptIssuer();

            var result = await CashHandler(store, bills, receipts).Handle(
                new ConfirmCashPaymentCommand { BillId = store.Bill.Id, Amount = 10.00m, IdempotencyKey = "cash-key" },
                default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Empty(bills.Closed);
            Assert.Empty(receipts.Issued);
        }

        [Fact]
        public void ReceiptNumbersRestartEachDayAndStayUniqueWithinADay()
        {
            var firstDay = new DateOnly(2026, 11, 24);
            var secondDay = new DateOnly(2026, 11, 25);

            var dayOne = Enumerable.Range(1, 3)
                .Select(number => Receipt.FormatReceiptNumber(firstDay, number))
                .ToArray();
            var nextDay = Receipt.FormatReceiptNumber(secondDay, 1);

            Assert.Equal(new[] { "R-20261124-1", "R-20261124-2", "R-20261124-3" }, dayOne);
            Assert.Equal(3, dayOne.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal("R-20261125-1", nextDay);
            Assert.NotEqual(dayOne[0], nextDay);
            Assert.All(dayOne.Append(nextDay), number => Assert.True(Receipt.IsValidReceiptNumber(number)));
            Assert.False(Receipt.IsValidReceiptNumber("R-2026112-1"));
            Assert.False(Receipt.IsValidReceiptNumber("R-20261124-0"));
            Assert.False(Receipt.IsValidReceiptNumber("X-20261124-1"));
        }

        [Fact]
        public void AWifiLineIsCopiedIntoTheReceiptSnapshot()
        {
            var bill = Bill.Create(Guid.CreateVersion7(), BranchId, null, Reference, Now, null);
            var item = OrderItem.CreateWifiLine(
                Guid.CreateVersion7(),
                1,
                Guid.CreateVersion7(),
                "One Hour",
                60,
                1m,
                5m,
                1,
                Now,
                UserId);
            var order = Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                null,
                1,
                new DateOnly(2026, 11, 24),
                OrderServiceType.WifiOnly,
                OrderSource.Guest,
                "Sara",
                null,
                null,
                Reference,
                "wifi-key",
                null,
                Now.AddMinutes(-5),
                Now.AddMinutes(5),
                [item],
                null);
            order.AcceptAutomatically(Now.AddMinutes(-4));
            order.AttachToBill(bill.Id);
            var payment = Payment.ConfirmCash(
                Guid.CreateVersion7(), bill.Id, null, 5m, UserId, "cash-key", Now, UserId);

            var receipt = ReceiptComposer.Compose(
                Guid.CreateVersion7(),
                bill,
                [order],
                [payment],
                Receipt.FormatReceiptNumber(new DateOnly(2026, 11, 24), 9),
                Now,
                UserId);

            var line = Assert.Single(receipt.Lines);
            Assert.Equal("One Hour", line.ProductName);
            Assert.Equal("WiFi", line.SizeName);
            Assert.Equal(1, line.Quantity);
            Assert.Equal(5.00m, line.UnitPrice);
            Assert.Equal(5.00m, line.LineTotal);
            Assert.Equal(5.00m, receipt.TotalAmount);
        }

        private static Order AcceptedOrder(
            Bill bill,
            string? guestDisplayName,
            string productName,
            string sizeName,
            decimal unitPrice,
            int quantity,
            IReadOnlyCollection<OrderItemAddon> addons,
            int orderNumber = 1)
        {
            var order = Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                TableId,
                orderNumber,
                new DateOnly(2026, 11, 24),
                OrderServiceType.DineIn,
                OrderSource.Guest,
                guestDisplayName ?? string.Empty,
                "device-identifier-0001",
                null,
                Reference,
                $"idempotency-{Guid.NewGuid():N}",
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
                    sizeName,
                    unitPrice,
                    quantity,
                    addons,
                    Now.AddMinutes(-5),
                    null)],
                null);
            order.Accept(UserId, Now.AddMinutes(-1));
            order.AttachToBill(bill.Id);
            return order;
        }

        private static OrderItemAddon Addon(string name, decimal price) =>
            OrderItemAddon.Create(Guid.CreateVersion7(), Guid.NewGuid(), name, price, Now, null);

        private static string MediaReference() => $"tenants/{BranchId:D}/{Guid.NewGuid():N}";

        private static ConfirmCashPaymentCommandHandler CashHandler(
            FakePaymentStore store,
            FakeBillStore bills,
            FakeReceiptIssuer receipts) =>
            new(store, bills, receipts, new FakeTenantUser(), new FixedClock(Now));

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => ReceiptCreationTests.UserId;

            public Guid? TenantId => BranchId;

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => ReceiptCreationTests.BranchId;

            public TenantAccountAccess? AccountAccess { get; } =
                new(true, null, true) { BranchId = ReceiptCreationTests.BranchId };
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FakePaymentStore : IPaymentStore
        {
            private readonly List<Order> orders = [];
            private readonly List<Payment> payments = [];

            public Bill Bill { get; } =
                Bill.Create(Guid.CreateVersion7(), BranchId, TableId, Reference, Now, null);

            public void Track(Order order) => orders.Add(order);

            public void SeedPendingProof(decimal claimedAmount) =>
                payments.Add(Payment.SubmitOnlineProof(
                    Guid.CreateVersion7(), Bill.Id, claimedAmount, MediaReference(), null, "proof-key", Now));

            public Task<BillSettlement?> FindSettlementAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                Task.FromResult<BillSettlement?>(
                    billId == Bill.Id && (branchId is null || branchId == Bill.BranchId)
                        ? new BillSettlement(Bill, orders.ToList(), payments.ToList())
                        : null);

            public Task<PaymentReview?> FindReviewAsync(
                Guid? branchId,
                Guid paymentId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The receipt flow must not review a payment.");

            public Task<BillSettlement?> FindForProofAsync(
                string billReference,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The receipt flow must not submit a proof.");

            public Task<Result<Payment>> AddProofAsync(Payment payment, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The receipt flow must not submit a proof.");

            public Task<Result<Payment>> SaveReviewAsync(
                Payment payment,
                string? rejectionReason,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The receipt flow must not review a payment.");

            public Task<Result<Payment>> AddAsync(Payment payment, CancellationToken cancellationToken)
            {
                var stored = payments.FirstOrDefault(candidate => candidate.IdempotencyKey == payment.IdempotencyKey);
                if (stored is not null)
                {
                    return Task.FromResult(Result.Success(stored));
                }

                payments.Add(payment);
                return Task.FromResult(Result.Success(payment));
            }
        }

        private sealed class FakeBillStore : IBillStore
        {
            public List<Bill> Closed { get; } = [];

            public Task<Result<Bill>> ResolveOpenBillAsync(
                Guid branchId,
                Guid? tableId,
                DateTimeOffset now,
                Guid? actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The receipt flow must not open a bill.");

            public Task<BillSettlement?> FindForCloseAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The receipt flow must not read a bill for closing.");

            public Task<Result> SaveCloseAsync(
                Bill bill,
                decimal remainingAmount,
                Guid actorId,
                CancellationToken cancellationToken)
            {
                Closed.Add(bill);
                return Task.FromResult(Result.Success());
            }
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
    }
}
