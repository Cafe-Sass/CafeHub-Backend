using CafeHub.Application.Pagination;

namespace CafeHub.UnitTests
{
    public sealed class PaginationRequestValidatorTests
    {
        private static readonly PaginationOptions Options = new();

        [Fact]
        public void OffsetPageRequestValidator_AcceptsAnUnconfiguredRequest_WithTheDocumentedDefaults()
        {
            var validator = new OffsetPageRequestValidator(Options);

            Assert.True(validator.Validate(new OffsetPageRequest()).IsValid);
        }

        [Fact]
        public void OffsetPageRequestValidator_AcceptsAPageSizeEqualToTheMaximum()
        {
            var options = new PaginationOptions { MaxPageSize = 100 };
            var validator = new OffsetPageRequestValidator(options);

            var result = validator.Validate(new OffsetPageRequest { Page = 1, PageSize = 100 });

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(101)]
        [InlineData(0)]
        [InlineData(-5)]
        public void OffsetPageRequestValidator_RejectsOutOfRangePageSizes_WithTheExpectedCodes(int invalidPageSize)
        {
            var validator = new OffsetPageRequestValidator(Options);

            var result = validator.Validate(new OffsetPageRequest { Page = 2, PageSize = invalidPageSize });

            Assert.False(result.IsValid);
            var failure = result.Errors.Single();
            Assert.Equal("pageSize", failure.PropertyName);
            Assert.Equal(
                invalidPageSize > 100 ? PaginationErrors.PageSizeTooLargeCode : PaginationErrors.PageSizeInvalidCode,
                failure.ErrorCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-2)]
        public void OffsetPageRequestValidator_RejectsPagesBelowOne_WithThePageInvalidCode(int invalidPage)
        {
            var validator = new OffsetPageRequestValidator(Options);

            var result = validator.Validate(new OffsetPageRequest { Page = invalidPage, PageSize = 20 });

            Assert.False(result.IsValid);
            var failure = result.Errors.Single();
            Assert.Equal("page", failure.PropertyName);
            Assert.Equal(PaginationErrors.PageInvalidCode, failure.ErrorCode);
        }

        [Fact]
        public void KeysetPageRequestValidator_AcceptsANullCursor_AsTheFirstPage()
        {
            var validator = new KeysetPageRequestValidator(Options);

            Assert.True(validator.Validate(new KeysetPageRequest()).IsValid);
        }

        [Fact]
        public void KeysetPageRequestValidator_AcceptsAPageSizeEqualToTheMaximum()
        {
            var options = new PaginationOptions { MaxPageSize = 100 };
            var validator = new KeysetPageRequestValidator(options);

            var result = validator.Validate(new KeysetPageRequest { Cursor = "opaque-cursor-text", PageSize = 100 });

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(101)]
        [InlineData(0)]
        [InlineData(-5)]
        public void KeysetPageRequestValidator_RejectsOutOfRangePageSizes_WithTheExpectedCodes(int invalidPageSize)
        {
            var validator = new KeysetPageRequestValidator(Options);

            var result = validator.Validate(new KeysetPageRequest { Cursor = "opaque-cursor-text", PageSize = invalidPageSize });

            Assert.False(result.IsValid);
            var failure = result.Errors.Single();
            Assert.Equal("pageSize", failure.PropertyName);
            Assert.Equal(
                invalidPageSize > 100 ? PaginationErrors.PageSizeTooLargeCode : PaginationErrors.PageSizeInvalidCode,
                failure.ErrorCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void KeysetPageRequestValidator_RejectsAnEmptyCursor_WithTheCursorInvalidCode(string emptyCursor)
        {
            var validator = new KeysetPageRequestValidator(Options);

            var result = validator.Validate(new KeysetPageRequest { Cursor = emptyCursor, PageSize = 20 });

            Assert.False(result.IsValid);
            var failure = result.Errors.Single();
            Assert.Equal("cursor", failure.PropertyName);
            Assert.Equal(PaginationErrors.CursorInvalidCode, failure.ErrorCode);
        }
    }
}
