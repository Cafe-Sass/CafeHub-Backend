using System.Globalization;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Refunds
{
    public sealed class RefundRequestTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid StaffId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Create_ValidatesFieldsAndCopiesBillAndPaymentScope()
        {
            var bill = CreateBill();
            var payment = CreatePayment(bill, Guid.CreateVersion7());

            var request = RefundRequest.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                bill,
                payment,
                new string('A', RefundRequest.PublicReferenceLength),
                "refund-123",
                " abc ",
                new string('d', RefundRequest.MaximumDetailsLength),
                payment.Amount,
                0m,
                " Guest ",
                Now);

            Assert.Equal(bill.Id, request.BillId);
            Assert.Equal(payment.Id, request.PaymentId);
            Assert.Equal(payment.OrderId, request.OrderId);
            Assert.Equal(bill.BranchId, request.BranchId);
            Assert.Equal("abc", request.Reason);
            Assert.Equal(RefundRequest.MaximumDetailsLength, request.Details?.Length);
            Assert.Equal("Guest", request.GuestDisplayName);
            Assert.Equal(RefundRequestStatus.Open, request.Status);
            Assert.Null(request.CreatedBy);
            Assert.Null(request.UpdatedBy);
            Assert.Collection(
                request.Events,
                entry =>
                {
                    Assert.Equal(RefundRequestEventKind.Submitted, entry.Kind);
                    Assert.Null(entry.FromStatus);
                    Assert.Equal(RefundRequestStatus.Open, entry.ToStatus);
                    Assert.Equal(RefundRequestActorKind.Guest, entry.ActorKind);
                    Assert.Null(entry.ActorUserId);
                });
        }

        [Theory]
        [InlineData("abc", true)]
        [InlineData("  abc  ", true)]
        [InlineData("ab", false)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void ReasonMustBeThreeTo256CharactersAfterTrimming(string reason, bool expected)
        {
            Assert.Equal(expected, RefundRequest.IsValidReason(reason));
        }

        [Fact]
        public void ReasonAccepts256Characters()
        {
            Assert.True(RefundRequest.IsValidReason(new string('r', RefundRequest.MaximumReasonLength)));
        }

        [Fact]
        public void ReasonRejectsMoreThan256Characters()
        {
            Assert.False(RefundRequest.IsValidReason(new string('r', RefundRequest.MaximumReasonLength + 1)));
        }

        [Fact]
        public void DetailsAllowNullAnd2000CharactersButRejectLongerValues()
        {
            Assert.True(RefundRequest.IsValidDetails(null));
            Assert.True(RefundRequest.IsValidDetails(new string('d', RefundRequest.MaximumDetailsLength)));
            Assert.False(RefundRequest.IsValidDetails(new string('d', RefundRequest.MaximumDetailsLength + 1)));
        }

        [Theory]
        [InlineData("refund-1", true)]
        [InlineData("12345678", true)]
        [InlineData("1234567890123456789012345678901234567890123456789012345678901234", true)]
        [InlineData("short", false)]
        [InlineData("12345678901234567890123456789012345678901234567890123456789012345", false)]
        [InlineData("refund/key", false)]
        public void IdempotencyKeyUsesTheSharedUrlSafeContract(string key, bool expected)
        {
            Assert.Equal(expected, RefundRequest.IsValidIdempotencyKey(key));
        }

        [Theory]
        [InlineData("0", "0")]
        [InlineData("100.01", "0")]
        [InlineData("60", "-1")]
        [InlineData("60", "100.01")]
        public void CreateRejectsAmountsOutsideThePaymentRefundableBalance(
            string claimedAmount,
            string approvedRefundedAmount)
        {
            var bill = CreateBill();
            var payment = CreatePayment(bill);

            Assert.Throws<ArgumentOutOfRangeException>(() => CreateFromPayment(
                bill,
                payment,
                claimedAmount: decimal.Parse(claimedAmount, CultureInfo.InvariantCulture),
                approvedRefundedAmount: decimal.Parse(approvedRefundedAmount, CultureInfo.InvariantCulture)));
        }

        [Fact]
        public void CreateRejectsAClaimAboveThePaymentAmountAfterApprovedRefunds()
        {
            var bill = CreateBill();
            var payment = CreatePayment(bill);

            Assert.Throws<ArgumentOutOfRangeException>(() => CreateFromPayment(
                bill,
                payment,
                claimedAmount: 70.01m,
                approvedRefundedAmount: 30m));

            var withinRemaining = CreateFromPayment(
                bill,
                payment,
                claimedAmount: 70m,
                approvedRefundedAmount: 30m);
            Assert.Equal(70m, withinRemaining.ClaimedAmount);
        }

        [Fact]
        public void CreateRequiresAConfirmedPaymentBelongingToTheBill()
        {
            var bill = CreateBill();
            var otherBill = CreateBill();
            var payment = CreatePayment(bill);
            var pendingPayment = Payment.SubmitOnlineProof(
                Guid.CreateVersion7(),
                bill.Id,
                100m,
                "tenants/11111111-1111-1111-1111-111111111111/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                null,
                "payment-key",
                Now);

            Assert.Throws<ArgumentException>(() => CreateFromPayment(otherBill, payment));
            Assert.Throws<ArgumentException>(() => CreateFromPayment(bill, pendingPayment));
        }

        [Theory]
        [InlineData(RefundRequestStatus.Open, RefundRequestStatus.InReview)]
        [InlineData(RefundRequestStatus.Open, RefundRequestStatus.Approved)]
        [InlineData(RefundRequestStatus.Open, RefundRequestStatus.Rejected)]
        [InlineData(RefundRequestStatus.InReview, RefundRequestStatus.Approved)]
        [InlineData(RefundRequestStatus.InReview, RefundRequestStatus.Rejected)]
        public void EveryAllowedTransitionUpdatesStateAndAppendsAStaffEvent(
            RefundRequestStatus from,
            RefundRequestStatus target)
        {
            Assert.True(RefundRequestTransitions.IsAllowed(from, target));
            var request = CreateRequest();
            if (from == RefundRequestStatus.InReview)
            {
                Assert.True(request.TransitionTo(
                    Guid.CreateVersion7(), from, StaffId, null, Now.AddMinutes(1)));
            }

            var resolutionNote = target is RefundRequestStatus.Approved or RefundRequestStatus.Rejected
                ? "Reviewed"
                : null;
            Assert.True(request.TransitionTo(
                Guid.CreateVersion7(), target, StaffId, resolutionNote, Now.AddMinutes(2)));

            Assert.Equal(target, request.Status);
            Assert.Equal(Now.AddMinutes(2), request.UpdatedAt);
            Assert.Equal(StaffId, request.UpdatedBy);
            Assert.Equal(from == RefundRequestStatus.Open ? 2 : 3, request.Events.Count);
            Assert.Equal(RefundRequestActorKind.Staff, request.Events[^1].ActorKind);
            Assert.Equal(from, request.Events[^1].FromStatus);
            Assert.Equal(target, request.Events[^1].ToStatus);
        }

        [Theory]
        [InlineData(RefundRequestStatus.Open, RefundRequestStatus.Open)]
        [InlineData(RefundRequestStatus.InReview, RefundRequestStatus.Open)]
        [InlineData(RefundRequestStatus.Approved, RefundRequestStatus.InReview)]
        [InlineData(RefundRequestStatus.Approved, RefundRequestStatus.Rejected)]
        [InlineData(RefundRequestStatus.Rejected, RefundRequestStatus.Approved)]
        public void ForbiddenTransitionsAreRefusedWithoutChangingStateOrHistory(
            RefundRequestStatus from,
            RefundRequestStatus target)
        {
            var request = CreateRequest();
            if (from == RefundRequestStatus.InReview)
            {
                Assert.True(request.TransitionTo(
                    Guid.CreateVersion7(), from, StaffId, null, Now.AddMinutes(1)));
            }
            else if (from is RefundRequestStatus.Approved or RefundRequestStatus.Rejected)
            {
                Assert.True(request.TransitionTo(
                    Guid.CreateVersion7(), from, StaffId, "Reviewed", Now.AddMinutes(1)));
            }

            var historyCount = request.Events.Count;
            var changedAt = request.UpdatedAt;

            Assert.False(request.TransitionTo(
                Guid.CreateVersion7(), target, StaffId, "Reviewed", Now.AddMinutes(2)));

            Assert.Equal(from, request.Status);
            Assert.Equal(historyCount, request.Events.Count);
            Assert.Equal(changedAt, request.UpdatedAt);
        }

        [Theory]
        [InlineData(RefundRequestStatus.Approved)]
        [InlineData(RefundRequestStatus.Rejected)]
        public void DecisionsRequireAResolutionNote(RefundRequestStatus target)
        {
            var request = CreateRequest();

            Assert.False(request.TransitionTo(Guid.CreateVersion7(), target, StaffId, null, Now));
            Assert.False(request.TransitionTo(Guid.CreateVersion7(), target, StaffId, "  ", Now));
            Assert.Equal(RefundRequestStatus.Open, request.Status);
            Assert.Null(request.ResolutionNote);
            Assert.Null(request.DecidedAt);

            Assert.True(request.TransitionTo(Guid.CreateVersion7(), target, StaffId, " resolved ", Now));
            Assert.Equal("resolved", request.ResolutionNote);
            Assert.Equal(StaffId, request.DecidedByUserId);
            Assert.Equal(Now, request.DecidedAt);
        }

        [Fact]
        public void AddAttachmentAcceptsFiveAndRefusesTheSixth()
        {
            var request = CreateRequest();
            var attachments = Enumerable.Range(1, RefundRequest.MaximumAttachmentCount + 1)
                .Select(index => CreateAttachment(request.Id, index))
                .ToArray();

            foreach (var attachment in attachments.Take(RefundRequest.MaximumAttachmentCount))
            {
                Assert.True(request.AddAttachment(attachment));
            }

            Assert.False(request.AddAttachment(attachments[^1]));
            Assert.Equal(RefundRequest.MaximumAttachmentCount, request.Attachments.Count);
        }

        [Fact]
        public void AttachmentRejectsInvalidMediaMetadata()
        {
            Assert.Throws<ArgumentException>(() => RefundRequestAttachment.Create(
                Guid.CreateVersion7(), Guid.CreateVersion7(), "key", "text/plain", 10, new string('a', 64), Now));
            Assert.Throws<ArgumentOutOfRangeException>(() => RefundRequestAttachment.Create(
                Guid.CreateVersion7(), Guid.CreateVersion7(), "key", "application/pdf", 0, new string('a', 64), Now));
            Assert.Throws<ArgumentException>(() => RefundRequestAttachment.Create(
                Guid.CreateVersion7(), Guid.CreateVersion7(), "key", "application/pdf", 10, new string('A', 64), Now));
        }

        private static RefundRequest CreateRequest(
            Bill? bill = null,
            Payment? payment = null,
            decimal claimedAmount = 100m,
            decimal approvedRefundedAmount = 0m)
        {
            bill ??= CreateBill();
            payment ??= CreatePayment(bill);
            return CreateFromPayment(bill, payment, claimedAmount, approvedRefundedAmount);
        }

        private static RefundRequest CreateFromPayment(
            Bill bill,
            Payment payment,
            decimal claimedAmount = 100m,
            decimal approvedRefundedAmount = 0m) =>
            RefundRequest.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                bill,
                payment,
                new string('A', RefundRequest.PublicReferenceLength),
                "refund-123",
                "Valid reason",
                null,
                claimedAmount,
                approvedRefundedAmount,
                null,
                Now);

        private static Bill CreateBill() => Bill.Create(
            Guid.CreateVersion7(),
            BranchId,
            null,
            new string('B', Bill.PublicReferenceLength),
            Now,
            null);

        private static Payment CreatePayment(Bill bill, Guid? orderId = null) => Payment.ConfirmCash(
            Guid.CreateVersion7(),
            bill.Id,
            orderId,
            100m,
            StaffId,
            "payment-key",
            Now,
            StaffId);

        private static RefundRequestAttachment CreateAttachment(Guid requestId, int index) =>
            RefundRequestAttachment.Create(
                Guid.Parse($"{index:x8}-1111-1111-1111-111111111111"),
                requestId,
                $"tenants/{BranchId:D}/{index:x32}",
                "application/pdf",
                1024,
                new string('a', 64),
                Now.AddMinutes(index));
    }
}
