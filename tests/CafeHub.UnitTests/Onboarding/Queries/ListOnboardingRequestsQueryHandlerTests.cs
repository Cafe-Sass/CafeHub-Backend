using CafeHub.Application.Onboarding.DTOs;
using CafeHub.Application.Onboarding.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Onboarding;
using CafeHub.UnitTests.Onboarding.Application;
using FluentValidation.TestHelper;

namespace CafeHub.UnitTests.Onboarding.Queries
{
    public sealed class ListOnboardingRequestsQueryHandlerTests
    {
        private static readonly Guid PlanVersionId = Guid.CreateVersion7();
        private static readonly DateTimeOffset BaseTime = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        private static OnboardingRequest RequestInState(OnboardingRequestStatus status, DateTimeOffset createdAt)
        {
            var request = OnboardingRequest.CreateForPublicRegistration(
                Guid.CreateVersion7(),
                PlanVersionId,
                "owner@example.com",
                "Owner Name",
                null,
                "Cafe Name",
                "hash",
                "my-cafe",
                createdAt);
            request.Status = status;
            return request;
        }

        private static ListOnboardingRequestsQueryHandler BuildHandler(
            FakeOnboardingRequestStore store,
            PaginationOptions? paginationOptions = null)
        {
            return new ListOnboardingRequestsQueryHandler(store, paginationOptions ?? new PaginationOptions());
        }

        [Fact]
        public async Task Handle_Default_ReturnsOnlyPaidAwaitingReview()
        {
            var paid = RequestInState(OnboardingRequestStatus.PaidAwaitingReview, BaseTime);
            var awaiting = RequestInState(OnboardingRequestStatus.AwaitingPayment, BaseTime.AddMinutes(1));
            var approved = RequestInState(OnboardingRequestStatus.Approved, BaseTime.AddMinutes(2));
            var rejected = RequestInState(OnboardingRequestStatus.Rejected, BaseTime.AddMinutes(3));

            var store = new FakeOnboardingRequestStore(paid, awaiting, approved, rejected);
            var handler = BuildHandler(store);

            var result = await handler.Handle(new ListOnboardingRequestsQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var page = result.Value!;
            Assert.Single(page.Items);
            Assert.Equal(paid.Id, page.Items[0].RequestId);
            Assert.Equal(PlanVersionId, page.Items[0].PlanVersionId);
            Assert.Equal(OnboardingRequestStatus.PaidAwaitingReview.ToString(), page.Items[0].State);
            Assert.Equal(1, store.ListCallCount);
        }

        [Theory]
        [InlineData(OnboardingRequestStatus.Approved)]
        [InlineData(OnboardingRequestStatus.Rejected)]
        [InlineData(OnboardingRequestStatus.Cancelled)]
        public async Task Handle_StateFilter_ReturnsRequestedState(OnboardingRequestStatus status)
        {
            var matching = RequestInState(status, BaseTime);
            var paid = RequestInState(OnboardingRequestStatus.PaidAwaitingReview, BaseTime.AddMinutes(1));

            var store = new FakeOnboardingRequestStore(matching, paid);
            var handler = BuildHandler(store);

            var result = await handler.Handle(new ListOnboardingRequestsQuery { State = status.ToString() }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var page = result.Value!;
            Assert.Single(page.Items);
            Assert.Equal(matching.Id, page.Items[0].RequestId);
            Assert.Equal(status.ToString(), page.Items[0].State);
        }

        [Fact]
        public async Task Handle_DefaultSort_OldestFirst()
        {
            var newest = RequestInState(OnboardingRequestStatus.PaidAwaitingReview, BaseTime.AddMinutes(10));
            var oldest = RequestInState(OnboardingRequestStatus.PaidAwaitingReview, BaseTime);
            var middle = RequestInState(OnboardingRequestStatus.PaidAwaitingReview, BaseTime.AddMinutes(5));

            var store = new FakeOnboardingRequestStore(newest, oldest, middle);
            var handler = BuildHandler(store);

            var result = await handler.Handle(new ListOnboardingRequestsQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var page = result.Value!;
            Assert.Equal(3, page.Items.Count);
            Assert.Equal(oldest.Id, page.Items[0].RequestId);
            Assert.Equal(middle.Id, page.Items[1].RequestId);
            Assert.Equal(newest.Id, page.Items[2].RequestId);
        }

        [Fact]
        public async Task Handle_Paging_ReturnsPageSizeAndMetadata()
        {
            var requests = Enumerable
                .Range(0, 25)
                .Select(i => RequestInState(OnboardingRequestStatus.PaidAwaitingReview, BaseTime.AddMinutes(i)))
                .ToArray();

            var store = new FakeOnboardingRequestStore(requests);
            var handler = BuildHandler(store);

            var result = await handler.Handle(
                new ListOnboardingRequestsQuery { Page = 1, PageSize = 20, IncludeTotalCount = true },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var page = result.Value!;
            Assert.Equal(20, page.Items.Count);
            Assert.True(page.HasNextPage);
            Assert.Equal(25, page.TotalCount);
        }

        [Fact]
        public async Task Handle_Paging_LastPage_HasNoNextPage()
        {
            var requests = Enumerable
                .Range(0, 25)
                .Select(i => RequestInState(OnboardingRequestStatus.PaidAwaitingReview, BaseTime.AddMinutes(i)))
                .ToArray();

            var store = new FakeOnboardingRequestStore(requests);
            var handler = BuildHandler(store);

            var result = await handler.Handle(
                new ListOnboardingRequestsQuery { Page = 2, PageSize = 20, IncludeTotalCount = false },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var page = result.Value!;
            Assert.Equal(5, page.Items.Count);
            Assert.False(page.HasNextPage);
            Assert.Null(page.TotalCount);
        }

        [Fact]
        public void Validator_RejectsUnknownState()
        {
            var validator = new ListOnboardingRequestsQueryValidator(new PaginationOptions());
            var query = new ListOnboardingRequestsQuery { State = "UnknownState" };

            var result = validator.TestValidate(query);

            Assert.Contains(result.Errors, error => error.PropertyName == "state");
        }

        [Fact]
        public void Validator_RejectsUnknownSortField()
        {
            var validator = new ListOnboardingRequestsQueryValidator(new PaginationOptions());
            var query = new ListOnboardingRequestsQuery { Sort = "notAField" };

            var result = validator.TestValidate(query);

            Assert.Contains(result.Errors, error => error.PropertyName == "sort");
        }

        [Fact]
        public void Validator_RejectsOversizedPageSize()
        {
            var validator = new ListOnboardingRequestsQueryValidator(new PaginationOptions());
            var query = new ListOnboardingRequestsQuery { PageSize = 200 };

            var result = validator.TestValidate(query);

            Assert.Contains(result.Errors, error => error.PropertyName == "pageSize");
        }

        [Fact]
        public void ProjectionTypes_DoNotExposeHashKeyOrProviderMembers()
        {
            foreach (var type in new[] { typeof(OnboardingRequestListItem), typeof(OnboardingRequestDetail) })
            {
                foreach (var property in type.GetProperties())
                {
                    var name = property.Name;
                    Assert.False(name.Contains("password", StringComparison.OrdinalIgnoreCase));
                    Assert.False(name.Contains("hash", StringComparison.OrdinalIgnoreCase));
                    Assert.False(name.Contains("idempotency", StringComparison.OrdinalIgnoreCase));
                    Assert.False(name.Contains("provider", StringComparison.OrdinalIgnoreCase));
                }
            }
        }
    }
}
