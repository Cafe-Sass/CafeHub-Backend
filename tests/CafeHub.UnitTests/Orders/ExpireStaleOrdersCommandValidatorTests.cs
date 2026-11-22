using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Commands;
using FluentValidation.Results;

namespace CafeHub.UnitTests.Orders
{
    public sealed class ExpireStaleOrdersCommandValidatorTests
    {
        private readonly ExpireStaleOrdersCommandValidator validator = new();

        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OrderId = Guid.Parse("99999999-9999-9999-9999-999999999999");

        [Fact]
        public void ABranchScopeIsValid()
        {
            Assert.True(Validate(new ExpireStaleOrdersCommand(BranchId, null)).IsValid);
        }

        [Fact]
        public void AnOrderScopeIsValid()
        {
            Assert.True(Validate(new ExpireStaleOrdersCommand(null, OrderId)).IsValid);
        }

        [Fact]
        public void NoScopeIsRefused()
        {
            Assert.False(Validate(new ExpireStaleOrdersCommand(null, null)).IsValid);
        }

        [Fact]
        public void BothScopesAreRefused()
        {
            Assert.False(Validate(new ExpireStaleOrdersCommand(BranchId, OrderId)).IsValid);
        }

        [Fact]
        public void EmptyScopeIdentifiersAreRefused()
        {
            Assert.False(Validate(new ExpireStaleOrdersCommand(Guid.Empty, null)).IsValid);
            Assert.False(Validate(new ExpireStaleOrdersCommand(null, Guid.Empty)).IsValid);
        }

        private ValidationResult Validate(ExpireStaleOrdersCommand command) => validator.Validate(command);
    }
}
