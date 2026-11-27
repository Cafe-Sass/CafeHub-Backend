using CafeHub.Application.Refunds;
using CafeHub.Application.Refunds.Contracts;
using CafeHub.Application.Refunds.DTOs;
using CafeHub.Application.Refunds.Queries;

namespace CafeHub.UnitTests.Refunds
{
    public sealed class GuestRefundRequestQueryTests
    {
        private const string BillReference = "Abcdefghijklmnopqrstuv";

        [Fact]
        public async Task ConfirmedPaymentsQuery_ReturnsOnlyTheGuestProjection()
        {
            var expected = new GuestRefundablePayment(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "Cash",
                12.50m,
                new DateTimeOffset(2026, 11, 27, 9, 0, 0, TimeSpan.Zero));
            var store = new FakeRefundRequestStore { ConfirmedPayments = [expected] };

            var result = await new GetGuestBillPaymentsQueryHandler(store)
                .Handle(new GetGuestBillPaymentsQuery(BillReference), default);

            Assert.True(result.IsSuccess);
            Assert.Equal([expected], result.Value);
            Assert.Equal(BillReference, store.BillReference);
        }

        [Theory]
        [InlineData("short")]
        [InlineData("ZYXWVUTSRQPONMLKJIHGFE")]
        public async Task ConfirmedPaymentsQuery_MalformedOrUnknownBillUsesNotFound(string reference)
        {
            var store = new FakeRefundRequestStore();

            var result = await new GetGuestBillPaymentsQueryHandler(store)
                .Handle(new GetGuestBillPaymentsQuery(reference), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(reference == "short" ? 0 : 1, store.ConfirmedPaymentsCalls);
        }

        [Fact]
        public async Task RefundStatusQuery_ReturnsOnlyPublicStatusAndDecisionNote()
        {
            var expected = new GuestRefundRequestStatus(BillReference, "Approved", "Reviewed");
            var store = new FakeRefundRequestStore { GuestStatus = expected };

            var result = await new GetGuestRefundRequestStatusQueryHandler(store)
                .Handle(new GetGuestRefundRequestStatusQuery(BillReference), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(expected, result.Value);
            Assert.Equal(BillReference, store.PublicReference);
        }

        [Theory]
        [InlineData("short")]
        [InlineData("ZYXWVUTSRQPONMLKJIHGFE")]
        public async Task RefundStatusQuery_MalformedOrUnknownReferenceUsesNotFound(string reference)
        {
            var store = new FakeRefundRequestStore();

            var result = await new GetGuestRefundRequestStatusQueryHandler(store)
                .Handle(new GetGuestRefundRequestStatusQuery(reference), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(RefundRequestErrors.NotFoundCode, result.Error!.Code);
            Assert.Equal(reference == "short" ? 0 : 1, store.StatusCalls);
        }

        private sealed class FakeRefundRequestStore : IRefundRequestStore
        {
            public IReadOnlyList<GuestRefundablePayment>? ConfirmedPayments { get; init; }

            public GuestRefundRequestStatus? GuestStatus { get; init; }

            public int ConfirmedPaymentsCalls { get; private set; }

            public int StatusCalls { get; private set; }

            public string? BillReference { get; private set; }

            public string? PublicReference { get; private set; }

            public Task<IReadOnlyList<GuestRefundablePayment>?> ListConfirmedPaymentsForBillAsync(
                string billReference,
                CancellationToken cancellationToken)
            {
                ConfirmedPaymentsCalls++;
                BillReference = billReference;
                return Task.FromResult(ConfirmedPayments);
            }

            public Task<GuestRefundRequestStatus?> FindGuestStatusAsync(
                string publicReference,
                CancellationToken cancellationToken)
            {
                StatusCalls++;
                PublicReference = publicReference;
                return Task.FromResult(GuestStatus);
            }
        }
    }
}
