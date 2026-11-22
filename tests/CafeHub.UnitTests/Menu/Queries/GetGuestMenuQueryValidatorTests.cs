using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Menu.Queries
{
    public sealed class GetGuestMenuQueryValidatorTests
    {
        private readonly GetGuestMenuQueryValidator validator =
            new(new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

        [Fact]
        public void DefaultRequest_IsValid()
        {
            Assert.True(validator.Validate(new GetGuestMenuQuery()).IsValid);
        }

        [Fact]
        public void KnownFilters_AreValid()
        {
            var query = new GetGuestMenuQuery
            {
                Search = "latte",
                CategoryId = Guid.NewGuid(),
                Kind = ProductKind.Drink,
                Caffeine = true,
                Tag = "hot",
                Page = 2,
                PageSize = 50,
                IncludeTotalCount = true,
                Sort = "name",
                SortDirection = SortDirection.Desc,
            };

            Assert.True(validator.Validate(query).IsValid);
        }

        [Fact]
        public void UnknownSortValue_IsRejected()
        {
            var result = validator.Validate(new GetGuestMenuQuery { Sort = "bestselling" });

            var failure = Assert.Single(result.Errors);
            Assert.Equal("sort", failure.PropertyName);
            Assert.Equal("Pagination.SortFieldNotAllowed", failure.ErrorCode);
        }

        [Fact]
        public void UndefinedKind_IsRejected()
        {
            var result = validator.Validate(new GetGuestMenuQuery { Kind = (ProductKind)99 });

            var failure = Assert.Single(result.Errors);
            Assert.Equal("kind", failure.PropertyName);
            Assert.Equal("GuestMenu.InvalidKind", failure.ErrorCode);
        }

        [Fact]
        public void InvalidTag_IsRejected()
        {
            var result = validator.Validate(new GetGuestMenuQuery { Tag = "Hot!" });

            var failure = Assert.Single(result.Errors);
            Assert.Equal("tag", failure.PropertyName);
            Assert.Equal("GuestMenu.InvalidTag", failure.ErrorCode);
        }

        [Fact]
        public void PageSizeAboveTheConfiguredMaximum_IsRejected()
        {
            var result = validator.Validate(new GetGuestMenuQuery { PageSize = 101 });

            var failure = Assert.Single(result.Errors);
            Assert.Equal("pageSize", failure.PropertyName);
        }

        [Fact]
        public void PageBelowOne_IsRejected()
        {
            var result = validator.Validate(new GetGuestMenuQuery { Page = 0 });

            var failure = Assert.Single(result.Errors);
            Assert.Equal("page", failure.PropertyName);
        }
    }
}
