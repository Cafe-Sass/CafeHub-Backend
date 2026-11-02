using CafeHub.Application.Branches;
using CafeHub.Application.Branches.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Branches.Queries
{
    public sealed class ListBranchesQueryValidatorTests
    {
        private readonly ListBranchesQueryValidator validator = new(new PaginationOptions());

        [Fact]
        public void Validate_WithTheDefaults_IsValid()
        {
            var result = validator.Validate(new ListBranchesQuery());

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAPageSizeEqualToTheMaximum_IsValid()
        {
            var result = validator.Validate(new ListBranchesQuery { PageSize = 100 });

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void Validate_WithAPageBelowOne_ReturnsThePageInvalidFailure(int page)
        {
            var result = validator.Validate(new ListBranchesQuery { Page = page });

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
            var result = validator.Validate(new ListBranchesQuery { PageSize = pageSize });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("pageSize", failure.PropertyName);
            Assert.Equal(PaginationErrors.PageSizeInvalidCode, failure.ErrorCode);
        }

        [Fact]
        public void Validate_WithAPageSizeAboveTheMaximum_ReturnsThePageSizeTooLargeFailure()
        {
            var result = validator.Validate(new ListBranchesQuery { PageSize = 101 });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("pageSize", failure.PropertyName);
            Assert.Equal(PaginationErrors.PageSizeTooLargeCode, failure.ErrorCode);
        }

        [Theory]
        [InlineData("name")]
        [InlineData("NAME")]
        [InlineData("createdAt")]
        [InlineData("id")]
        public void Validate_WithAWhitelistedSortField_IsValid(string sort)
        {
            var result = validator.Validate(new ListBranchesQuery { Sort = sort });

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAnUnknownSortField_ReturnsTheSortFieldFailure()
        {
            var result = validator.Validate(new ListBranchesQuery { Sort = "lifecycleStatus" });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("sort", failure.PropertyName);
            Assert.Equal(PaginationErrors.SortFieldNotAllowedCode, failure.ErrorCode);
        }

        [Fact]
        public void Validate_WithAnUndefinedSortDirectionAndNoSort_ReturnsTheSortDirectionFailure()
        {
            var result = validator.Validate(new ListBranchesQuery { SortDirection = (SortDirection)99 });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("sort", failure.PropertyName);
            Assert.Equal(PaginationErrors.SortDirectionInvalidCode, failure.ErrorCode);
        }

        [Fact]
        public void Validate_WithAValidStatus_IsValid()
        {
            var result = validator.Validate(new ListBranchesQuery { Status = BranchLifecycleStatus.Active });

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Validate_WithAnInvalidStatus_ReturnsTheStatusFailureWithoutEchoingTheValue()
        {
            var result = validator.Validate(new ListBranchesQuery { Status = (BranchLifecycleStatus)99 });

            Assert.False(result.IsValid);
            var failure = Assert.Single(result.Errors);
            Assert.Equal("status", failure.PropertyName);
            Assert.Equal(BranchesErrors.InvalidStatusCode, failure.ErrorCode);
            Assert.DoesNotContain("99", failure.ErrorMessage, StringComparison.Ordinal);
        }
    }
}
