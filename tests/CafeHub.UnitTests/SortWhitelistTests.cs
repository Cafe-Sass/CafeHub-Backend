using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Pagination;

namespace CafeHub.UnitTests
{
    public sealed class SortWhitelistTests
    {
        private static SortWhitelist CreateTestWhitelist(
            SortDirection defaultDirection = SortDirection.Desc,
            string defaultField = "recorded_at")
        {
            return SortWhitelist.Create(
                ["recorded_at", "action_code", "id"],
                defaultField,
                defaultDirection);
        }

        [Fact]
        public void Create_RequiresTheIdentifierFieldToBeRequestable()
        {
            var exception = Record.Exception(() => SortWhitelist.Create(["name", "code"], "name", SortDirection.Asc));

            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void Create_RejectsAnEmptyWhitelist()
        {
            var exception = Record.Exception(() => SortWhitelist.Create([], "id", SortDirection.Asc));

            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void Create_RejectsCaseInsensitiveDuplicateFields()
        {
            var whitelist = Record.Exception(() => SortWhitelist.Create(["Name", "name"], "name", SortDirection.Asc));

            Assert.IsType<ArgumentException>(whitelist);
        }

        [Fact]
        public void Create_RejectsADefaultSortOutsideTheWhitelist()
        {
            var exception = Record.Exception(() => SortWhitelist.Create(["name", "id"], "code", SortDirection.Asc));

            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void Resolve_RejectsAnUnknownField_WithTheSortFieldNotAllowedCode()
        {
            var whitelist = CreateTestWhitelist();

            var resolved = whitelist.Resolve([new SortRequest("owner_name", SortDirection.Asc)]);

            Assert.True(resolved.IsFailure);
            Assert.Equal(PaginationErrors.SortFieldNotAllowedCode, resolved.Error!.Code);
            Assert.Equal(ErrorType.Validation, resolved.Error!.Type);
            Assert.Contains("owner_name", resolved.Error!.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_MatchesCaseInsensitively_AndReturnsTheCanonicalField()
        {
            var whitelist = SortWhitelist.Create(
                ["DisplayName", "CreatedAt", "id"],
                "CreatedAt",
                SortDirection.Asc);

            var resolved = whitelist.Resolve([new SortRequest("displayNAME", SortDirection.Asc)]);

            Assert.True(resolved.IsSuccess);
            Assert.Equal("DisplayName", resolved.Value[0].Field);
            Assert.Equal("id", resolved.Value[^1].Field);
        }

        [Fact]
        public void Resolve_AppendsTheIdentifierTiebreaker_WithTheLastRequestedDirection()
        {
            var whitelist = CreateTestWhitelist();

            var resolved = whitelist.Resolve(
            [
                new SortRequest("recorded_at", SortDirection.Asc),
                new SortRequest("action_code", SortDirection.Desc),
            ]);

            Assert.True(resolved.IsSuccess);
            Assert.Equal(
                new[]
                {
                    new SortTerm("recorded_at", SortDirection.Asc),
                    new SortTerm("action_code", SortDirection.Desc),
                    new SortTerm("id", SortDirection.Desc),
                },
                resolved.Value);
        }

        [Fact]
        public void Resolve_RequestedIdentifier_IsNotDuplicated()
        {
            var whitelist = CreateTestWhitelist();

            var resolved = whitelist.Resolve([new SortRequest("id", SortDirection.Desc)]);

            Assert.True(resolved.IsSuccess);
            var terms = resolved.Value;
            Assert.Single(terms);
            Assert.Equal(new SortTerm("id", SortDirection.Desc), terms[0]);
        }

        [Fact]
        public void Resolve_EmptySort_UsesTheWhitelistDefaultPlusTheIdentifier()
        {
            var whitelist = CreateTestWhitelist();

            var resolved = whitelist.Resolve([]);

            Assert.True(resolved.IsSuccess);
            Assert.Equal(
                new[]
                {
                    new SortTerm("recorded_at", SortDirection.Desc),
                    new SortTerm("id", SortDirection.Desc),
                },
                resolved.Value);
        }

        [Theory]
        [InlineData(SortDirection.Asc)]
        [InlineData(SortDirection.Desc)]
        public void Resolve_EmptySort_TiebreakerFollowsTheDefaultSortDirection(SortDirection defaultDirection)
        {
            var whitelist = CreateTestWhitelist(defaultDirection: defaultDirection);

            var resolved = whitelist.Resolve(null);

            Assert.True(resolved.IsSuccess);
            Assert.Equal(defaultDirection, resolved.Value[^1].Direction);
        }

        [Fact]
        public void SortRequestValidator_RejectsAFieldOutsideTheWhitelist_OnTheSortField()
        {
            var whitelist = CreateTestWhitelist();
            var validator = new SortRequestValidator(whitelist);

            var result = validator.Validate(new SortRequest("tenant_slug", SortDirection.Asc));

            Assert.False(result.IsValid);
            var failure = result.Errors.Single();
            Assert.Equal(PaginationErrors.SortFieldNotAllowedCode, failure.ErrorCode);
            Assert.Equal("sort", failure.PropertyName);
            Assert.Contains("tenant_slug", failure.ErrorMessage, StringComparison.Ordinal);
        }

        [Fact]
        public void SortRequestValidator_RejectsAnUndefinedDirection_OnTheSortField()
        {
            var whitelist = CreateTestWhitelist();
            var validator = new SortRequestValidator(whitelist);

            var result = validator.Validate(new SortRequest("id", (SortDirection)9));

            Assert.False(result.IsValid);
            var failure = result.Errors.Single();
            Assert.Equal(PaginationErrors.SortDirectionInvalidCode, failure.ErrorCode);
            Assert.Equal("sort", failure.PropertyName);
        }

        [Fact]
        public void SortRequestValidator_AcceptsAWhitelistedRequest()
        {
            var whitelist = CreateTestWhitelist();
            var validator = new SortRequestValidator(whitelist);

            var result = validator.Validate(new SortRequest("ACTION_CODE", SortDirection.Desc));

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
    }
}
