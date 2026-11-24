using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Billing;
using CafeHub.Application.Billing.Commands;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.Queries;
using CafeHub.Application.Identity;
using CafeHub.Application.Media;
using CafeHub.Application.Media.Contracts;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.UnitTests.Media;
using FluentValidation.TestHelper;

namespace CafeHub.UnitTests.Billing
{
    public sealed class PaymentReviewTests
    {
        private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid OtherBranchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid TableId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-24T12:00:00Z");
        private const string Reference = "Abcdefghijklmnopqrstuv";
        private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

        [Fact]
        public async Task Confirm_RecordsTheConfirmedAmountActorAndTime()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var payment = store.SeedPendingProof(8.00m);
            var handler = ConfirmHandler(store);

            var result = await handler.Handle(
                new ConfirmPaymentCommand { PaymentId = payment.Id, Amount = 6.00m }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal("Confirmed", result.Value!.Status);
            Assert.Equal(6.00m, result.Value.Amount);
            Assert.Equal(8.00m, result.Value.ClaimedAmount);
            Assert.Equal(6.00m, result.Value.BillPaidAmount);
            Assert.Equal(4.00m, result.Value.BillRemainingAmount);
            Assert.Equal(Now, result.Value.ConfirmedAt);
            Assert.Equal(PaymentStatus.Confirmed, payment.Status);
            Assert.Equal(6.00m, payment.Amount);
            Assert.Equal(8.00m, payment.ClaimedAmount);
            Assert.Equal(UserId, payment.ConfirmedByUserId);
            Assert.Equal(Now, payment.ConfirmedAt);
        }

        [Fact]
        public async Task Confirm_DefaultsToTheClaimedAmount()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var payment = store.SeedPendingProof(8.00m);

            var result = await ConfirmHandler(store).Handle(
                new ConfirmPaymentCommand { PaymentId = payment.Id }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(8.00m, result.Value!.Amount);
            Assert.Equal(2.00m, result.Value.BillRemainingAmount);
        }

        [Fact]
        public async Task Confirm_AnAmountAboveTheRemainingIsRefused()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            store.SeedConfirmedCash(6.00m);
            var payment = store.SeedPendingProof(5.00m);

            var result = await ConfirmHandler(store).Handle(
                new ConfirmPaymentCommand { PaymentId = payment.Id }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(BillingErrors.AmountAboveRemainingCode, result.Error!.Code);
            Assert.Equal(PaymentStatus.PendingReview, payment.Status);
            Assert.Equal(0, store.ReviewWrites);
        }

        [Fact]
        public async Task Confirm_ARepeatIsIdempotentAndWritesOnlyOneDecision()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var payment = store.SeedPendingProof(5.00m);
            var handler = ConfirmHandler(store);

            var first = await handler.Handle(
                new ConfirmPaymentCommand { PaymentId = payment.Id }, default);
            var repeat = await handler.Handle(
                new ConfirmPaymentCommand { PaymentId = payment.Id, Amount = 2.00m }, default);

            Assert.True(first.IsSuccess, first.Error?.Code);
            Assert.True(repeat.IsSuccess, repeat.Error?.Code);
            Assert.Equal(first.Value!.Amount, repeat.Value!.Amount);
            Assert.Equal(5.00m, repeat.Value.BillPaidAmount);
            Assert.Equal(1, store.ReviewWrites);
        }

        [Fact]
        public async Task Reject_StoresTheReasonAndLeavesTheRemainderPayable()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var payment = store.SeedPendingProof(5.00m);
            var handler = RejectHandler(store);

            var result = await handler.Handle(
                new RejectPaymentCommand { PaymentId = payment.Id, Reason = "  blurry proof  " }, default);
            var repeat = await handler.Handle(
                new RejectPaymentCommand { PaymentId = payment.Id, Reason = "blurry proof" }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.True(repeat.IsSuccess, repeat.Error?.Code);
            Assert.Equal("Rejected", result.Value!.Status);
            Assert.Equal(0m, result.Value.BillPaidAmount);
            Assert.Equal(10.00m, result.Value.BillRemainingAmount);
            Assert.Equal(PaymentStatus.Rejected, payment.Status);
            Assert.Equal("blurry proof", store.LastRejectionReason);
            Assert.Equal(1, store.ReviewWrites);
        }

        [Fact]
        public async Task Decide_TerminalPaymentsAreRefusedAndTheStatusIsNamed()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var payable = store.SeedPendingProof(5.00m);
            var rejectable = store.SeedPendingProof(5.00m);
            await ConfirmHandler(store).Handle(new ConfirmPaymentCommand { PaymentId = payable.Id }, default);
            await RejectHandler(store).Handle(
                new RejectPaymentCommand { PaymentId = rejectable.Id, Reason = "not paid" }, default);

            var confirmedReject = await RejectHandler(store).Handle(
                new RejectPaymentCommand { PaymentId = payable.Id, Reason = "late" }, default);
            var rejectedConfirm = await ConfirmHandler(store).Handle(
                new ConfirmPaymentCommand { PaymentId = rejectable.Id }, default);

            Assert.False(confirmedReject.IsSuccess);
            Assert.Equal(BillingErrors.PaymentNotPendingReviewCode, confirmedReject.Error!.Code);
            Assert.Contains("Confirmed", confirmedReject.Error.Message, StringComparison.Ordinal);
            Assert.False(rejectedConfirm.IsSuccess);
            Assert.Equal(BillingErrors.PaymentNotPendingReviewCode, rejectedConfirm.Error!.Code);
            Assert.Contains("Rejected", rejectedConfirm.Error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Confirm_ThePaymentThatClearsTheRemainingAmountClosesTheBill()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(10.00m));
            var payment = store.SeedPendingProof(10.00m);
            var bills = new FakeBillStore();
            var receipts = new FakeReceiptIssuer();

            var result = await ConfirmHandler(store, bills, receipts).Handle(
                new ConfirmPaymentCommand { PaymentId = payment.Id }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(0m, result.Value!.BillRemainingAmount);
            var closed = Assert.Single(bills.Closed);
            Assert.Equal(BillStatus.Closed, closed.Status);
            Assert.Equal(UserId, closed.ClosedByUserId);
            Assert.Equal(0m, Assert.Single(bills.ClosedRemaining));
            Assert.Single(receipts.Issued);
        }

        [Fact]
        public async Task Decide_TheBillStaysOpenWhileAnotherProofIsPending()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(5.00m));
            var payment = store.SeedPendingProof(5.00m);
            store.SeedPendingProof(5.00m);
            var bills = new FakeBillStore();

            var result = await ConfirmHandler(store, bills).Handle(
                new ConfirmPaymentCommand { PaymentId = payment.Id }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Equal(0m, result.Value!.BillRemainingAmount);
            Assert.Empty(bills.Closed);
            Assert.Equal(BillStatus.Open, store.FindBill()!.Status);
        }

        [Fact]
        public async Task Reject_TheLastPendingProofAtZeroRemainingClosesTheBill()
        {
            var store = new FakePaymentStore();
            store.SeedOpenBillWithAcceptedOrders(AcceptedOrder(5.00m));
            store.SeedConfirmedCash(5.00m);
            var payment = store.SeedPendingProof(5.00m);
            var bills = new FakeBillStore();

            var result = await RejectHandler(store, bills).Handle(
                new RejectPaymentCommand { PaymentId = payment.Id, Reason = "duplicate" }, default);

            Assert.True(result.IsSuccess, result.Error?.Code);
            Assert.Single(bills.Closed);
        }

        [Fact]
        public void Hybrid_CountsOnlyConfirmedOnlinePayments()
        {
            var cash = Payment.ConfirmCash(
                Guid.CreateVersion7(), Guid.CreateVersion7(), null, 4.00m, UserId, "cash-key", Now, UserId);
            var confirmed = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(), Guid.CreateVersion7(), 2.00m, MediaReference(), null, "online-1", Now);
            confirmed.ConfirmReview(2.00m, UserId, Now);
            var pending = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(), Guid.CreateVersion7(), 3.00m, MediaReference(), null, "online-2", Now);
            Payment[] payments = [cash, confirmed, pending];

            Assert.Equal(6.00m, Payment.ResolvePaidAmount(payments));
            Assert.Equal(4.00m, Payment.ResolveBillRemainingAmount(10.00m, payments));
            Assert.Equal(PaymentSettlement.Hybrid, Payment.ResolveSettlementType(payments));
            Assert.Equal(2.00m, confirmed.Amount);
            Assert.Equal(3.00m, pending.Amount);
        }

        [Fact]
        public void Rejection_AuditHoldsOnlyTheChangedDecisionFields()
        {
            var payment = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(), Guid.CreateVersion7(), 5.00m, MediaReference(), "  secret note  ", "key-123", Now);
            payment.RejectReview("blurry proof", UserId, Now);

            var changes = TenantAuditChangeSets.PaymentReview(payment, "blurry proof");
            var fields = changes.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
            Assert.Equal("Rejected", fields[nameof(Payment.Status)].After);
            Assert.Equal("blurry proof", fields["RejectionReason"].After);
            Assert.DoesNotContain(nameof(Payment.Note), fields.Keys);
            Assert.DoesNotContain(nameof(Payment.MediaReference), fields.Keys);

            var snapshot = TenantAuditSnapshotSerializer.Create("payments", changes);
            Assert.True(snapshot.IsSuccess, snapshot.Error?.Code);
            var afterState = snapshot.Value.AfterState!;
            Assert.Contains("\"RejectionReason\":\"blurry proof\"", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("secret note", afterState, StringComparison.Ordinal);
            Assert.DoesNotContain("key-123", afterState, StringComparison.Ordinal);
        }

        [Fact]
        public void ConfirmAudit_DoesNotLeakTheGuestNoteOrMediaKey()
        {
            var payment = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(), Guid.CreateVersion7(), 5.00m, MediaReference(), "guest note", "key-123", Now);
            payment.ConfirmReview(4.00m, UserId, Now);

            var changes = TenantAuditChangeSets.PaymentReview(payment, null);
            var fields = changes.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
            Assert.Equal("Confirmed", fields[nameof(Payment.Status)].After);
            Assert.Equal("5.00", fields[nameof(Payment.Amount)].Before);
            Assert.Equal("4.00", fields[nameof(Payment.Amount)].After);
            Assert.Equal(Now.ToString("O"), fields[nameof(Payment.ConfirmedAt)].After);
            Assert.DoesNotContain("RejectionReason", fields.Keys);
            Assert.DoesNotContain("guest note", snapshot(changes), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Proof_ReturnsTheDetectedContentTypeAndRefusesForeignBranches()
        {
            var payment = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(), BranchId, 5.00m, MediaReference(), null, "key-123", Now);
            var store = new FakePaymentQueryStore(BranchId, payment);
            var storage = new FakeMediaStorage();
            var key = MediaKey.TryParse(payment.MediaReference, out var parsed) ? parsed! : throw new InvalidOperationException();
            using (var content = new MemoryStream(Png))
            {
                await storage.SaveAsync(key, content, default);
            }

            var handler = new GetPaymentProofQueryHandler(store, storage, StaffUser(), new FakeTenantContext());
            var found = await handler.Handle(new GetPaymentProofQuery(payment.Id), default);
            var foreign = await handler.Handle(new GetPaymentProofQuery(Guid.CreateVersion7()), default);

            Assert.True(found.IsSuccess, found.Error?.Code);
            Assert.Equal("image/png", found.Value!.ContentType);
            await found.Value.Content.DisposeAsync();
            Assert.False(foreign.IsSuccess);
            Assert.Equal(BillingErrors.PaymentNotFoundCode, foreign.Error!.Code);
        }

        [Fact]
        public async Task List_ScopesThePageToTheStaffBranchAndMapsTheRows()
        {
            var billId = Guid.CreateVersion7();
            var store = new FakePaymentQueryStore(
                BranchId,
                Payment.SubmitOnlineProof(
                    Guid.CreateVersion7(), billId, 5.00m, MediaReference(), "note", "key-123", Now));

            var scoped = await CreateListHandler(store).Handle(new GetPaymentsQuery(), default);
            var mismatch = await CreateListHandler(store).Handle(
                new GetPaymentsQuery { BranchId = OtherBranchId }, default);
            var owner = await CreateListHandler(store, StaffUser(isStaff: false)).Handle(
                new GetPaymentsQuery(), default);

            Assert.True(scoped.IsSuccess, scoped.Error?.Code);
            Assert.Single(scoped.Value!.Items);
            Assert.Equal("PendingReview", scoped.Value.Items[0].Status);
            Assert.Equal(BranchId, store.LastRequest!.BranchId);
            Assert.False(mismatch.IsSuccess);
            Assert.Equal(BillingErrors.NotFoundCode, mismatch.Error!.Code);
            Assert.False(owner.IsSuccess);
            Assert.Equal(BillingErrors.BranchRequiredCode, owner.Error!.Code);
        }

        [Fact]
        public void Validators_RefuseBadDecisions()
        {
            var invalid = new ConfirmPaymentCommandValidator()
                .TestValidate(new ConfirmPaymentCommand { PaymentId = Guid.Empty, Amount = 0m });
            invalid.ShouldHaveValidationErrorFor(command => command.PaymentId);
            invalid.ShouldHaveValidationErrorFor(command => command.Amount);
            new ConfirmPaymentCommandValidator()
                .TestValidate(new ConfirmPaymentCommand { PaymentId = Guid.CreateVersion7(), Amount = 1.005m })
                .ShouldHaveValidationErrorFor(command => command.Amount);

            var rejectValidator = new RejectPaymentCommandValidator();
            rejectValidator.TestValidate(new RejectPaymentCommand { PaymentId = Guid.CreateVersion7(), Reason = " " })
                .ShouldHaveValidationErrorFor(command => command.Reason);
            rejectValidator.TestValidate(new RejectPaymentCommand { PaymentId = Guid.CreateVersion7(), Reason = "ab" })
                .ShouldHaveValidationErrorFor(command => command.Reason);
            rejectValidator.TestValidate(new RejectPaymentCommand
            {
                PaymentId = Guid.CreateVersion7(),
                Reason = new string('a', Payment.MaximumRejectionReasonLength + 1),
            })
                .ShouldHaveValidationErrorFor(command => command.Reason);
            rejectValidator.TestValidate(new RejectPaymentCommand
            {
                PaymentId = Guid.CreateVersion7(),
                Reason = "blurry\u0001proof",
            })
                .ShouldHaveValidationErrorFor(command => command.Reason);
            rejectValidator.TestValidate(new RejectPaymentCommand
            {
                PaymentId = Guid.CreateVersion7(),
                Reason = "blurry proof",
            })
                .ShouldNotHaveAnyValidationErrors();
        }

        private static string snapshot(TenantAuditChanges changes) =>
            TenantAuditSnapshotSerializer.Create("payments", changes).Value.AfterState!;

        private static ConfirmPaymentCommandHandler ConfirmHandler(
            FakePaymentStore store,
            FakeBillStore? bills = null,
            FakeReceiptIssuer? receipts = null) =>
            new(store, bills ?? new FakeBillStore(), receipts ?? new FakeReceiptIssuer(), StaffUser(), new FixedClock(Now));

        private static RejectPaymentCommandHandler RejectHandler(
            FakePaymentStore store,
            FakeBillStore? bills = null) =>
            new(store, bills ?? new FakeBillStore(), new FakeReceiptIssuer(), StaffUser(), new FixedClock(Now));

        private static GetPaymentsQueryHandler CreateListHandler(
            FakePaymentQueryStore store,
            FakeTenantUser? user = null) =>
            new(store, user ?? StaffUser(), new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

        private static FakeTenantUser StaffUser(bool isStaff = true) =>
            new(
                UserId,
                isStaff
                    ? new TenantAccountAccess(true, null, true) { BranchId = BranchId }
                    : new TenantAccountAccess(false, null, true));

        private static string MediaReference() => $"tenants/{TenantId:D}/{Guid.NewGuid():N}";

        private static Order AcceptedOrder(decimal lineTotal) =>
            Order.Create(
                Guid.CreateVersion7(),
                BranchId,
                TableId,
                1,
                new DateOnly(2026, 11, 24),
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

        private sealed class FakeTenantContext : ITenantContextAccessor
        {
            public TenantContext GetRequired() => TenantContext.Create(TenantId, "tenant-secret", true);
        }

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => PaymentReviewTests.TenantId;

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
                Guid tableId,
                DateTimeOffset now,
                Guid? actorId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The review flow must not open a bill.");

            public Task<BillSettlement?> FindForCloseAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The review flow must not read a bill for closing.");

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
            private readonly List<Bill> bills = [];
            private readonly Dictionary<Guid, List<Order>> orders = [];
            private readonly Dictionary<Guid, List<Payment>> payments = [];

            public int ReviewWrites { get; private set; }

            public string? LastRejectionReason { get; private set; }

            public Bill SeedOpenBillWithAcceptedOrders(Order order)
            {
                var bill = Bill.Create(Guid.CreateVersion7(), order.BranchId, order.TableId, Reference, Now, null);
                bills.Add(bill);
                order.Accept(UserId, Now);
                order.AttachToBill(bill.Id);
                orders[bill.Id] = [order];
                payments[bill.Id] = [];
                return bill;
            }

            public Payment SeedPendingProof(decimal claimedAmount)
            {
                var bill = bills[^1];
                var payment = Payment.SubmitOnlineProof(
                    Guid.CreateVersion7(),
                    bill.Id,
                    claimedAmount,
                    MediaReference(),
                    null,
                    $"proof-{Guid.NewGuid():N}",
                    Now);
                payments[bill.Id].Add(payment);
                return payment;
            }

            public void SeedConfirmedCash(decimal amount)
            {
                var bill = bills[^1];
                payments[bill.Id].Add(Payment.ConfirmCash(
                    Guid.CreateVersion7(), bill.Id, null, amount, UserId, $"cash-{Guid.NewGuid():N}", Now, UserId));
            }

            public Bill? FindBill() => bills.LastOrDefault();

            public Task<BillSettlement?> FindSettlementAsync(
                Guid? branchId,
                Guid billId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The review flow reads by payment id.");

            public Task<BillSettlement?> FindForProofAsync(
                string billReference,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The review flow does not submit a proof.");

            public Task<PaymentReview?> FindReviewAsync(
                Guid? branchId,
                Guid paymentId,
                CancellationToken cancellationToken)
            {
                foreach (var bill in bills)
                {
                    if (branchId is { } branch && bill.BranchId != branch)
                    {
                        continue;
                    }

                    var payment = payments[bill.Id].FirstOrDefault(candidate => candidate.Id == paymentId);
                    if (payment is not null)
                    {
                        return Task.FromResult<PaymentReview?>(new PaymentReview(
                            new BillSettlement(bill, orders[bill.Id], payments[bill.Id]),
                            payment));
                    }
                }

                return Task.FromResult<PaymentReview?>(null);
            }

            public Task<Result<Payment>> AddProofAsync(Payment payment, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The review flow does not submit a proof.");

            public Task<Result<Payment>> SaveReviewAsync(
                Payment payment,
                string? rejectionReason,
                CancellationToken cancellationToken)
            {
                ReviewWrites++;
                LastRejectionReason = rejectionReason;
                return Task.FromResult(Result.Success(payment));
            }

            public Task<Result<Payment>> AddAsync(Payment payment, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The review flow does not confirm cash.");
        }

        private sealed class FakePaymentQueryStore(Guid branchId, Payment payment) : IPaymentQueryStore
        {
            public PaymentListRequest? LastRequest { get; private set; }

            public Task<PaymentListPage> ListAsync(
                PaymentListRequest request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new PaymentListPage(
                    [
                        new PaymentListEntry(
                            payment.Id,
                            payment.BillId,
                            payment.Method,
                            payment.Status,
                            payment.Amount,
                            payment.ClaimedAmount,
                            payment.Note,
                            payment.CreatedAt,
                            payment.ConfirmedAt),
                    ],
                    null));
            }

            public Task<Payment?> FindProofAsync(
                Guid? scopedBranchId,
                Guid paymentId,
                CancellationToken cancellationToken) =>
                Task.FromResult(scopedBranchId is null || scopedBranchId == branchId
                    ? payment.Id == paymentId ? payment : null
                    : null);
        }
    }
}
