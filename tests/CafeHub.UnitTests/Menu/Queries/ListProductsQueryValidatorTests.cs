using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Menu.Queries
{
    public sealed class ListProductsQueryValidatorTests
    {
        private readonly ListProductsQueryValidator validator = new(
            new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

        [Fact]
        public void ValidQuery_Passes()
        {
            var result = validator.Validate(new ListProductsQuery
            {
                Page = 1,
                PageSize = 10,
                CategoryId = Guid.NewGuid(),
                Kind = ProductKind.Food,
                Caffeine = true,
                Tag = "hot",
                Status = ProductStatus.Active,
                Search = "latte",
            });

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("kind")]
        [InlineData("status")]
        public void InvalidEnum_Fails(string property)
        {
            var query = property == "kind"
                ? new ListProductsQuery { Page = 1, Kind = (ProductKind)99 }
                : new ListProductsQuery { Page = 1, Status = (ProductStatus)99 };

            var result = validator.Validate(query);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == property);
        }

        private const string TooLongTag = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Theory]
        [InlineData("under_score")]
        [InlineData("caf\u00e9")]
        [InlineData(TooLongTag)]
        public void InvalidTag_Fails(string tag)
        {
            var result = validator.Validate(new ListProductsQuery { Page = 1, Tag = tag });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "tag");
        }

        [Fact]
        public void UnknownSortField_Fails()
        {
            var result = validator.Validate(new ListProductsQuery { Page = 1, Sort = "bestselling" });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "sort");
        }
    }
}
