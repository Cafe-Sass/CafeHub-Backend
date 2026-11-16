using CafeHub.Application.Pagination;
using CafeHub.Application.Tables.Queries;

namespace CafeHub.UnitTests.Tables.Queries
{
    public sealed class TableQueryValidatorTests
    {
        [Theory]
        [InlineData(1, true)]
        [InlineData(9999, true)]
        [InlineData(0, false)]
        [InlineData(10000, false)]
        [InlineData(-1, false)]
        public void FromNumber_EnforcesTheNumberRange(int fromNumber, bool accepted)
        {
            var validator = new ListTablesQueryValidator(new PaginationOptions());
            var result = validator.Validate(new ListTablesQuery
            {
                BranchId = Guid.NewGuid(),
                Page = 1,
                PageSize = 20,
                IncludeTotalCount = false,
                FromNumber = fromNumber,
            });
            Assert.Equal(accepted, result.IsValid);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(9999, true)]
        [InlineData(0, false)]
        [InlineData(10000, false)]
        public void ToNumber_EnforcesTheNumberRange(int toNumber, bool accepted)
        {
            var validator = new ListTablesQueryValidator(new PaginationOptions());
            var result = validator.Validate(new ListTablesQuery
            {
                BranchId = Guid.NewGuid(),
                Page = 1,
                PageSize = 20,
                IncludeTotalCount = false,
                ToNumber = toNumber,
            });
            Assert.Equal(accepted, result.IsValid);
        }

        [Theory]
        [InlineData(5, 3, false)]
        [InlineData(3, 5, true)]
        [InlineData(3, 3, true)]
        public void FromNumber_MustNotBeGreaterThanToNumber(int fromNumber, int toNumber, bool accepted)
        {
            var validator = new ListTablesQueryValidator(new PaginationOptions());
            var result = validator.Validate(new ListTablesQuery
            {
                BranchId = Guid.NewGuid(),
                Page = 1,
                PageSize = 20,
                IncludeTotalCount = false,
                FromNumber = fromNumber,
                ToNumber = toNumber,
            });
            Assert.Equal(accepted, result.IsValid);
        }

        [Theory]
        [InlineData("Active", true)]
        [InlineData("Inactive", true)]
        [InlineData(null, true)]
        public void Status_MustBeADefinedStatus(string? status, bool accepted)
        {
            var validator = new ListTablesQueryValidator(new PaginationOptions());
            var result = validator.Validate(new ListTablesQuery
            {
                BranchId = Guid.NewGuid(),
                Page = 1,
                PageSize = 20,
                IncludeTotalCount = false,
                Status = status is null ? null : Enum.Parse<CafeHub.Domain.Tenants.BranchTableStatus>(status),
            });
            Assert.Equal(accepted, result.IsValid);
        }

        [Fact]
        public void Status_RejectsAnUndefinedEnumValue()
        {
            var validator = new ListTablesQueryValidator(new PaginationOptions());
            var result = validator.Validate(new ListTablesQuery
            {
                BranchId = Guid.NewGuid(),
                Page = 1,
                PageSize = 20,
                IncludeTotalCount = false,
                Status = (CafeHub.Domain.Tenants.BranchTableStatus)999,
            });
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(1, true)]
        [InlineData(100, true)]
        public void Page_EnforcesPositivePage(int page, bool accepted)
        {
            var validator = new ListTablesQueryValidator(new PaginationOptions());
            var result = validator.Validate(new ListTablesQuery
            {
                BranchId = Guid.NewGuid(),
                Page = page,
                PageSize = 20,
                IncludeTotalCount = false,
            });
            Assert.Equal(accepted, result.IsValid);
        }

        [Fact]
        public void BranchId_IsRequired()
        {
            var validator = new ListTablesQueryValidator(new PaginationOptions());
            var result = validator.Validate(new ListTablesQuery
            {
                BranchId = Guid.Empty,
                Page = 1,
                PageSize = 20,
                IncludeTotalCount = false,
            });
            Assert.False(result.IsValid);
        }

        [Fact]
        public void TableId_IsRequiredForGet()
        {
            var validator = new GetTableQueryValidator();
            var result = validator.Validate(new GetTableQuery(Guid.Empty));
            Assert.False(result.IsValid);
        }

        [Fact]
        public void GetTableQuery_AcceptsAValidTableId()
        {
            var validator = new GetTableQueryValidator();
            var result = validator.Validate(new GetTableQuery(Guid.NewGuid()));
            Assert.True(result.IsValid);
        }
    }
}
