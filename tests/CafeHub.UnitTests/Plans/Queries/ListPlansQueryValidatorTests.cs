using CafeHub.Application.Pagination;
using CafeHub.Application.Plans.Queries;

namespace CafeHub.UnitTests.Plans.Queries
{
    public sealed class ListPlansQueryValidatorTests
    {
        private readonly ListPlansQueryValidator validator = new(new PaginationOptions());

        [Fact]
        public void Validate_WithTheDefaults_IsValid()
        {
            var result = validator.Validate(new ListPlansQuery());

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAPageSizeEqualToTheMaximum_IsValid()
        {
            var result = validator.Validate(new ListPlansQuery { PageSize = 100 });

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Validate_WithAPageBelowOne_ReturnsThePageInvalidFailure(int page)
        {
            var result = validator.Validate(new ListPlansQuery { Page = page });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("page", failure.PropertyName);
            Assert.Equal(PaginationErrors.PageInvalidCode, failure.ErrorCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Validate_WithAPageSizeBelowOne_ReturnsThePageSizeInvalidFailure(int pageSize)
        {
            var result = validator.Validate(new ListPlansQuery { PageSize = pageSize });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("pageSize", failure.PropertyName);
            Assert.Equal(PaginationErrors.PageSizeInvalidCode, failure.ErrorCode);
        }

        [Fact]
        public void Validate_WithAPageSizeAboveTheMaximum_ReturnsThePageSizeTooLargeFailure()
        {
            var result = validator.Validate(new ListPlansQuery { PageSize = 101 });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("pageSize", failure.PropertyName);
            Assert.Equal(PaginationErrors.PageSizeTooLargeCode, failure.ErrorCode);
        }

        [Theory]
        [InlineData("key")]
        [InlineData("KEY")]
        [InlineData("createdAt")]
        [InlineData("id")]
        public void Validate_WithAWhitelistedSortField_IsValid(string sort)
        {
            var result = validator.Validate(new ListPlansQuery { Sort = sort });

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAnUnknownSortField_ReturnsTheSortFieldFailure()
        {
            var result = validator.Validate(new ListPlansQuery { Sort = "name" });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("sort", failure.PropertyName);
            Assert.Equal(PaginationErrors.SortFieldNotAllowedCode, failure.ErrorCode);
        }

        [Fact]
        public void Validate_WithAnUndefinedSortDirectionAndNoSort_ReturnsTheSortDirectionFailure()
        {
            var result = validator.Validate(new ListPlansQuery { SortDirection = (SortDirection)99 });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("sort", failure.PropertyName);
            Assert.Equal(PaginationErrors.SortDirectionInvalidCode, failure.ErrorCode);
        }

        [Fact]
        public void Validate_WithAnUndefinedSortDirectionAndAWhitelistedSort_ReturnsTheSortDirectionFailure()
        {
            var result = validator.Validate(
                new ListPlansQuery { Sort = PlanListSort.KeyField, SortDirection = (SortDirection)99 });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("sort", failure.PropertyName);
            Assert.Equal(PaginationErrors.SortDirectionInvalidCode, failure.ErrorCode);
        }
    }
}
