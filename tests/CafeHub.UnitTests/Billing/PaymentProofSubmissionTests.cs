using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Media;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Media;
using FluentValidation.TestHelper;

namespace CafeHub.UnitTests.Billing
{
    public sealed class PaymentProofSubmissionTests
    {
        private const long MaxBytes = 1024;
        private const string Reference = "Abcdefghijklmnopqrstuv";
        private const string IdempotencyKey = "proof-key-0001";

        private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid TableId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-23T12:00:00Z");

        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
        private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();

        [Fact]
        public async Task Submit_AValidImageCreatesAPendingReviewOnlinePaymentWithItsProof()
        {
            var store = new FakePaymentStore();
            var order = store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var media = Storage();
            var handler = CreateHandler(store, media);

            var result = await handler.Handle(
                Command(4.50m, "  lunch  ", Png), default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal("PendingReview", result.Value!.Status);
            Assert.Equal(4.50m, result.Value.ClaimedAmount);
            var payment = Assert.Single(store.Payments(store.BillId));
            Assert.Equal(PaymentMethod.Online, payment.Method);
            Assert.Equal(PaymentStatus.PendingReview, payment.Status);
            Assert.Equal(4.50m, payment.ClaimedAmount);
            Assert.Equal("lunch", payment.Note);
            Assert.False(string.IsNullOrWhiteSpace(payment.MediaReference));
            Assert.Equal(OrderStatus.Accepted, order.Status);
            Assert.Equal(1, media.Count);
        }

        [Theory]
        [InlineData("pdf")]
        [InlineData("empty")]
        [InlineData("oversize")]
        public async Task Submit_ANonImageEmptyOrOversizeUploadIsRefusedWithoutAPayment(string kind)
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var media = Storage();
            var handler = CreateHandler(store, media);
            var content = kind switch
            {
                "pdf" => Pdf,
                "empty" => [],
                _ => OversizePng(),
            };

            var result = await handler.Handle(Command(4.50m, null, content), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                kind == "oversize" ? MediaErrors.TooLargeCode : MediaErrors.InvalidFileCode,
                result.Error!.Code);
            Assert.Empty(store.Payments(store.BillId));
            Assert.Equal(0, media.Count);
        }

        [Fact]
        public async Task Submit_AnAmountAboveTheRemainingIsRefused()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            store.SeedConfirmedPayment(6.00m, "cash-key-0001");
            var media = Storage();
            var handler = CreateHandler(store, media);

            var result = await handler.Handle(Command(4.01m, null, Png), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(BillingErrors.AmountAboveRemainingCode, result.Error!.Code);
            Assert.Equal(0, media.Count);
        }

        [Fact]
        public async Task Submit_TheFourthPendingProofIsRefused()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            store.SeedPendingProof(1.00m, "proof-key-0002");
            store.SeedPendingProof(1.00m, "proof-key-0003");
            store.SeedPendingProof(1.00m, "proof-key-0004");
            var media = Storage();
            var handler = CreateHandler(store, media);

            var result = await handler.Handle(Command(1.00m, null, Png), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(BillingErrors.PendingProofLimitReachedCode, result.Error!.Code);
            Assert.Equal(3, store.Payments(store.BillId).Count);
            Assert.Equal(0, media.Count);
        }

        [Fact]
        public async Task Submit_ARepeatWithTheSameIdempotencyKeyReturnsTheSameProofAndUploadsOnce()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var media = Storage();
            var handler = CreateHandler(store, media);
            var first = await handler.Handle(Command(4.50m, "lunch", Png), default);

            var repeat = await handler.Handle(Command(4.50m, "lunch", Png), default);

            Assert.True(first.IsSuccess, first.Error?.Code);
            Assert.True(repeat.IsSuccess, repeat.Error?.Code);
            Assert.Equal(first.Value!.PaymentId, repeat.Value!.PaymentId);
            Assert.Equal(4.50m, repeat.Value.ClaimedAmount);
            Assert.Equal(1, store.AddProofCalls);
            Assert.Equal(1, media.Count);
        }

        [Fact]
        public async Task Submit_AnUnknownBillIsTheUniformNotFound()
        {
            var store = new FakePaymentStore();
            var media = Storage();
            var handler = CreateHandler(store, media);
            var command = Command(4.50m, null, Png) with { BillReference = "ZYXWVUTSRQPONMLKJIHGFE" };

            var result = await handler.Handle(command, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(BillingErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(0, media.Count);
        }

        [Fact]
        public async Task Submit_AClosedBillIsTreatedAsTheUniformNotFound()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var settlement = await store.FindForProofAsync(Reference, default);
            settlement!.Bill.Close("Shift ended", UserId, Now);
            var media = Storage();
            var handler = CreateHandler(store, media);

            var result = await handler.Handle(Command(4.50m, null, Png), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(BillingErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(0, media.Count);
        }

        [Fact]
        public void Submit_TheValidatorRefusesAMissingKeyAnInvalidReferenceAndBadAmounts()
        {
            var validator = new SubmitPaymentProofCommandValidator();

            var missingKey = validator.TestValidate(Command(4.50m, null, Png) with { IdempotencyKey = null });
            missingKey.ShouldHaveValidationErrorFor(command => command.IdempotencyKey);

            var badReference = validator.TestValidate(Command(4.50m, null, Png) with { BillReference = "short" });
            badReference.ShouldHaveValidationErrorFor(command => command.BillReference);

            var zero = validator.TestValidate(Command(0m, null, Png));
            zero.ShouldHaveValidationErrorFor(command => command.ClaimedAmount);

            var negative = validator.TestValidate(Command(-1m, null, Png));
            negative.ShouldHaveValidationErrorFor(command => command.ClaimedAmount);

            var overPrecise = validator.TestValidate(Command(1.001m, null, Png));
            overPrecise.ShouldHaveValidationErrorFor(command => command.ClaimedAmount);

            var longNote = validator.TestValidate(Command(4.50m, new string('a', 141), Png));
            longNote.ShouldHaveValidationErrorFor(command => command.Note);

            var controlNote = validator.TestValidate(Command(4.50m, "tab\tinside", Png));
            controlNote.ShouldHaveValidationErrorFor(command => command.Note);

            var noImage = validator.TestValidate(Command(4.50m, null, Png) with { Content = null });
            noImage.ShouldHaveValidationErrorFor(command => command.Content);
        }

        [Fact]
        public void Submit_TheValidatorAcceptsAValidProofWithoutANote()
        {
            var validator = new SubmitPaymentProofCommandValidator();

            var result = validator.TestValidate(Command(4.50m, null, Png));

            result.ShouldNotHaveAnyValidationErrors();
        }

        private static SubmitPaymentProofCommandHandler CreateHandler(FakePaymentStore store, FakeMediaStorage media) =>
            new(
                store,
                new MediaUploadService(media, MaxBytes),
                new FakeTenantContextAccessor(TenantContext.Create(TenantId, "tenant-secret-ref", true)),
                new FixedClock(Now));

        private static SubmitPaymentProofCommand Command(decimal claimedAmount, string? note, byte[] content) =>
            new()
            {
                BillReference = Reference,
                ClaimedAmount = claimedAmount,
                Note = note,
                Content = new MemoryStream(content),
                IdempotencyKey = IdempotencyKey,
            };

        private static FakeMediaStorage Storage() => new();

        private static byte[] OversizePng()
        {
            var bytes = new byte[MaxBytes + 1];
            Png.CopyTo(bytes, 0);
            return bytes;
        }

        private static Order AcceptedOrder(decimal lineTotal) =>
            Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                TableId,
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
                    "Latte",
                    "Regular",
                    lineTotal,
                    1,
                    [],
                    Now.AddMinutes(-5),
                    null)],
                null);

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FakeTenantContextAccessor(TenantContext context) : ITenantContextAccessor
        {
            public TenantContext GetRequired() => context;
        }

        private sealed class FakePaymentStore : IPaymentStore
        {
            private readonly Dictionary<Guid, Bill> bills = [];
            private readonly Dictionary<Guid, List<Order>> orders = [];
            private readonly Dictionary<Guid, List<Payment>> payments = [];

            public Guid BillId { get; private set; }

            public int AddProofCalls { get; private set; }

            public IReadOnlyList<Payment> Payments(Guid billId) => payments.GetValueOrDefault(billId, []);

            public Order SeedOpenBillWithAcceptedOrders(params Order[] billOrders)
            {
                var bill = Bill.Create(Guid.CreateVersion7(), BranchId, TableId, Reference, Now, null);
                bills[bill.Id] = bill;
                BillId = bill.Id;

                var attached = new List<Order>();
                foreach (var order in billOrders)
                {
                    order.Accept(UserId, Now);
                    order.AttachToBill(bill.Id);
                    attached.Add(order);
                }

                orders[bill.Id] = attached;
                return billOrders[0];
            }

            public void SeedConfirmedPayment(decimal amount, string idempotencyKey)
            {
                var payment = Payment.ConfirmCash(
                    Guid.CreateVersion7(), BillId, null, amount, UserId, idempotencyKey, Now, UserId);
                Add(payment);
            }

            public void SeedPendingProof(decimal amount, string idempotencyKey)
            {
                var payment = Payment.SubmitOnlineProof(
                    Guid.CreateVersion7(), BillId, amount, $"media-{idempotencyKey}", null, idempotencyKey, Now);
                Add(payment);
            }

            public Task<BillSettlement?> FindForProofAsync(
                string billReference,
                CancellationToken cancellationToken)
            {
                var bill = bills.Values.FirstOrDefault(
                    candidate => candidate.PublicReference == billReference);
                return Task.FromResult(bill is null
                    ? null
                    : new BillSettlement(
                        bill,
                        orders.GetValueOrDefault(bill.Id, []).ToList(),
                        payments.GetValueOrDefault(bill.Id, []).ToList()));
            }

            public Task<Result<Payment>> AddProofAsync(Payment payment, CancellationToken cancellationToken)
            {
                AddProofCalls++;
                Add(payment);
                return Task.FromResult(Result.Success(payment));
            }

            public Task<BillSettlement?> FindSettlementAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The proof flow does not read the settlement by bill id.");

            public Task<Result<Payment>> AddAsync(Payment payment, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The proof flow never writes an audited payment.");

            private void Add(Payment payment)
            {
                if (!payments.TryGetValue(BillId, out var billPayments))
                {
                    billPayments = [];
                    payments[BillId] = billPayments;
                }

                billPayments.Add(payment);
            }
        }
    }
}
