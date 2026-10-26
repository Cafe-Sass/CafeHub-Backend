using CafeHub.Application.Onboarding;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Onboarding.Queries;
using CafeHub.Application.Pagination;
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

        public Task<OnboardingRequest?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(this.requests.FirstOrDefault(request => request.Id == id));
        }

        public Task UpdateAsync(OnboardingRequest request, CancellationToken cancellationToken)
        {
            UpdateCallCount++;
            var index = this.requests.FindIndex(r => r.Id == request.Id);
            if (index >= 0)
            {
                this.requests[index] = request;
            }
            return Task.CompletedTask;
        }

        public Task<OnboardingRequestListPage> ListForReviewAsync(OnboardingRequestListRequest request, CancellationToken cancellationToken)
        {
            ListCallCount++;

            var filtered = this.requests
                .Where(item => item.Status == request.State)
                .ToList();

            var sorted = ApplySort(filtered, request.Sort);

            var page = sorted
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize + 1)
                .ToList();

            int? totalCount = request.IncludeTotalCount ? filtered.Count : null;
            return Task.FromResult(new OnboardingRequestListPage(page, totalCount));
        }

        private static List<OnboardingRequest> ApplySort(List<OnboardingRequest> items, IReadOnlyList<SortTerm> sort)
        {
            if (sort is null || sort.Count == 0)
            {
                return items;
            }

            var ordered = OrderBy(items.AsQueryable(), sort[0]);
            for (var index = 1; index < sort.Count; index++)
            {
                ordered = ThenBy(ordered, sort[index]);
            }

            return ordered.ToList();
        }

        private static IOrderedQueryable<OnboardingRequest> OrderBy(IQueryable<OnboardingRequest> query, SortTerm term)
        {
            return (term.Field, term.Direction) switch
            {
                (OnboardingRequestListSort.CreatedAtField, SortDirection.Asc) => query.OrderBy(item => item.CreatedAt),
                (OnboardingRequestListSort.CreatedAtField, SortDirection.Desc) => query.OrderByDescending(item => item.CreatedAt),
                (OnboardingRequestListSort.StateField, SortDirection.Asc) => query.OrderBy(item => item.Status),
                (OnboardingRequestListSort.StateField, SortDirection.Desc) => query.OrderByDescending(item => item.Status),
                (SortWhitelist.IdentifierField, SortDirection.Asc) => query.OrderBy(item => item.Id),
                (SortWhitelist.IdentifierField, SortDirection.Desc) => query.OrderByDescending(item => item.Id),
                _ => query.OrderBy(item => item.Id),
            };
        }

        private static IOrderedQueryable<OnboardingRequest> ThenBy(IOrderedQueryable<OnboardingRequest> query, SortTerm term)
        {
            return (term.Field, term.Direction) switch
            {
                (OnboardingRequestListSort.CreatedAtField, SortDirection.Asc) => query.ThenBy(item => item.CreatedAt),
                (OnboardingRequestListSort.CreatedAtField, SortDirection.Desc) => query.ThenByDescending(item => item.CreatedAt),
                (OnboardingRequestListSort.StateField, SortDirection.Asc) => query.ThenBy(item => item.Status),
                (OnboardingRequestListSort.StateField, SortDirection.Desc) => query.ThenByDescending(item => item.Status),
                (SortWhitelist.IdentifierField, SortDirection.Asc) => query.ThenBy(item => item.Id),
                (SortWhitelist.IdentifierField, SortDirection.Desc) => query.ThenByDescending(item => item.Id),
                _ => query.ThenBy(item => item.Id),
            };
        }

        public IReadOnlyList<OnboardingRequest> Requests => this.requests.AsReadOnly();
        public int AddCallCount { get; private set; }
        public int ListCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }
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
