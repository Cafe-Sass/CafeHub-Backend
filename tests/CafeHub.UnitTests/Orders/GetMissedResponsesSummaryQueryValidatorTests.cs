using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Queries;
using CafeHub.Application.Pagination;
using FluentValidation.Results;

namespace CafeHub.UnitTests.Orders
{
    public sealed class GetMissedResponsesSummaryQueryValidatorTests
    {
        private readonly GetMissedResponsesSummaryQueryValidator validator =
            new(new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

        [Fact]
        public void ASingleDayRangeIsValid()
        {
            var date = new DateOnly(2026, 11, 22);

            Assert.True(Validate(new GetMissedResponsesSummaryQuery { From = date, To = date }).IsValid);
        }

        [Fact]
        public void AReversedRangeIsRefused()
        {
            var result = Validate(new GetMissedResponsesSummaryQuery
            {
                From = new DateOnly(2026, 11, 30),
                To = new DateOnly(2026, 11, 1),
            });

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.ErrorMessage == OrdersErrors.DateRangeInvalid().Message);
        }

        [Fact]
        public void PageZeroIsRefused()
        {
            Assert.False(Validate(new GetMissedResponsesSummaryQuery
            {
                Page = 0,
                From = new DateOnly(2026, 11, 1),
                To = new DateOnly(2026, 11, 30),
            }).IsValid);
        }

        private ValidationResult Validate(GetMissedResponsesSummaryQuery query) => validator.Validate(query);
    }
}
