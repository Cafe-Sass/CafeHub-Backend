using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Domain.Onboarding;

namespace CafeHub.UnitTests.Onboarding.Application
{
    public sealed class FakeOnboardingRequestStore : IOnboardingRequestStore
    {
        private readonly List<OnboardingRequest> requests;
        public bool SkipFirstFind { get; set; }
        private int findCallCount;

        public FakeOnboardingRequestStore(params OnboardingRequest[] requests)
        {
            this.requests = new List<OnboardingRequest>(requests);
        }

        public Task<OnboardingRequest?> FindPublicByEmailAsync(string emailLower, CancellationToken cancellationToken)
        {
            findCallCount++;
            if (SkipFirstFind && findCallCount == 1)
            {
                return Task.FromResult<OnboardingRequest?>(null);
            }

            var found = this.requests.FirstOrDefault(request =>
                string.Equals(request.OwnerEmail, emailLower, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found);
        }

        public Task AddAsync(OnboardingRequest request, CancellationToken cancellationToken)
        {
            this.requests.Add(request);
            AddCallCount++;
            return Task.CompletedTask;
        }

        public IReadOnlyList<OnboardingRequest> Requests => this.requests.AsReadOnly();
        public int AddCallCount { get; private set; }
    }

    public sealed class OnboardingRequestDuplicateTests
    {
        private static readonly Guid RequestId = Guid.CreateVersion7();
        private static readonly Guid PlanVersionId = Guid.CreateVersion7();

        private static OnboardingRequest RequestInState(OnboardingRequestStatus status)
        {
            var request = OnboardingRequest.CreateForPublicRegistration(
                RequestId,
                PlanVersionId,
                "Owner@Example.com",
                "Owner Name",
                null,
                "Cafe Name",
                "hash",
                "my-cafe",
                TestData.CreatedAt);
            request.Status = status;
            return request;
        }

        [Theory]
        [InlineData(OnboardingRequestStatus.AwaitingPayment)]
        [InlineData(OnboardingRequestStatus.PaymentFailed)]
        [InlineData(OnboardingRequestStatus.PaidAwaitingReview)]
        [InlineData(OnboardingRequestStatus.Approved)]
        [InlineData(OnboardingRequestStatus.Rejected)]
        [InlineData(OnboardingRequestStatus.Cancelled)]
        public async Task FindPublicByEmail_MatchesCaseInsensitively_ForEveryState(OnboardingRequestStatus status)
        {
            var existing = RequestInState(status);
            var store = new FakeOnboardingRequestStore(existing);

            var found = await store.FindPublicByEmailAsync("OWNER@EXAMPLE.COM", CancellationToken.None);

            Assert.NotNull(found);
            Assert.Equal(RequestId, found.Id);
            Assert.Equal(status, found.Status);
        }

        [Theory]
        [InlineData(OnboardingRequestStatus.AwaitingPayment)]
        [InlineData(OnboardingRequestStatus.Approved)]
        [InlineData(OnboardingRequestStatus.Cancelled)]
        public async Task DuplicateEmail_ResolvesToExistingRequest_ForEveryState(OnboardingRequestStatus status)
        {
            var existing = RequestInState(status);
            var store = new FakeOnboardingRequestStore(existing);

            var found = await store.FindPublicByEmailAsync("owner@example.com", CancellationToken.None);

            var outcome = new ExistingRequest(found!.Id, found.Status);

            Assert.Equal(existing.Id, outcome.RequestId);
            Assert.Equal(status, outcome.State);
        }

        [Fact]
        public async Task FindPublicByEmail_ReturnsNull_WhenNoRequestExists()
        {
            var store = new FakeOnboardingRequestStore();

            var found = await store.FindPublicByEmailAsync("unknown@example.com", CancellationToken.None);

            Assert.Null(found);
        }
    }

    internal static class TestData
    {
        public static readonly DateTimeOffset CreatedAt = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
    }
}
