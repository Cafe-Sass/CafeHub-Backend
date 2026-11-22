using CafeHub.Application.Menu.Commands;
using CafeHub.Domain.Tenants;
using FluentValidation.TestHelper;

namespace CafeHub.UnitTests.Menu.Commands
{
    public sealed class SetBranchProductAvailabilityCommandValidatorTests
    {
        private readonly SetBranchProductAvailabilityCommandValidator validator = new();

        [Fact]
        public void ManualAvailabilityForRealIdsIsValid()
        {
            var result = validator.TestValidate(
                new SetBranchProductAvailabilityCommand(Guid.NewGuid(), Guid.NewGuid(), false, "manual"));

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Fact]
        public void EmptyIdsAreRejected()
        {
            var result = validator.TestValidate(
                new SetBranchProductAvailabilityCommand(Guid.Empty, Guid.Empty, false, "manual"));

            result.ShouldHaveValidationErrorFor(command => command.BranchId);
            result.ShouldHaveValidationErrorFor(command => command.ProductId);
        }

        [Theory]
        [InlineData(BranchProductAvailabilityReasons.OutOfStock)]
        [InlineData(BranchProductAvailabilityReasons.Expired)]
        [InlineData("")]
        [InlineData("stock")]
        public void OnlyTheManualReasonIsAcceptedByTheManagementApi(string reason)
        {
            var result = validator.TestValidate(
                new SetBranchProductAvailabilityCommand(Guid.NewGuid(), Guid.NewGuid(), false, reason));

            result.ShouldHaveValidationErrorFor("reason");
        }
    }
}
