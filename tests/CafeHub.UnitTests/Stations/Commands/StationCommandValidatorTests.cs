using CafeHub.Application.Stations.Commands;
using CafeHub.Application.Stations.Queries;
using FluentValidation.TestHelper;

namespace CafeHub.UnitTests.Stations.Commands
{
    public sealed class StationCommandValidatorTests
    {
        private readonly CreateStationCommandValidator createValidator = new();
        private readonly UpdateStationCommandValidator updateValidator = new();
        private readonly DeactivateStationCommandValidator deactivateValidator = new();
        private readonly ReactivateStationCommandValidator reactivateValidator = new();

        [Fact]
        public void Create_AcceptsATrimmedSixtyFourCharacterNameAndZeroSortOrder()
        {
            var result = createValidator.TestValidate(
                new CreateStationCommand(Guid.NewGuid(), $"  {new string('a', 64)}  ", 0));

            result.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        public void Create_RejectsBlankNames(string name)
        {
            var result = createValidator.TestValidate(new CreateStationCommand(Guid.NewGuid(), name, 0));

            result.ShouldHaveValidationErrorFor(command => command.Name);
        }

        [Fact]
        public void Create_RejectsNamesLongerThanSixtyFourCharactersAndControlCharacters()
        {
            var tooLong = createValidator.TestValidate(
                new CreateStationCommand(Guid.NewGuid(), new string('a', 65), 0));
            var control = createValidator.TestValidate(
                new CreateStationCommand(Guid.NewGuid(), "Bar\u0000", 0));

            tooLong.ShouldHaveValidationErrorFor(command => command.Name);
            control.ShouldHaveValidationErrorFor(command => command.Name);
        }

        [Fact]
        public void Create_RejectsAnEmptyBranchAndANegativeSortOrder()
        {
            var result = createValidator.TestValidate(new CreateStationCommand(Guid.Empty, "Bar", -1));

            result.ShouldHaveValidationErrorFor(command => command.BranchId);
            result.ShouldHaveValidationErrorFor(command => command.SortOrder);
        }

        [Fact]
        public void Update_RequiresAStationANameAndAValidConcurrencyToken()
        {
            var missing = updateValidator.TestValidate(new UpdateStationCommand(Guid.Empty, "", -1, "stale"));

            missing.ShouldHaveValidationErrorFor(command => command.StationId);
            missing.ShouldHaveValidationErrorFor(command => command.Name);
            missing.ShouldHaveValidationErrorFor(command => command.SortOrder);
            missing.ShouldHaveValidationErrorFor(command => command.ConcurrencyToken);

            var valid = updateValidator.TestValidate(
                new UpdateStationCommand(Guid.NewGuid(), "Bar", 3, "42"));

            valid.ShouldNotHaveAnyValidationErrors();
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-number")]
        public void LifecycleCommands_RequireAValidConcurrencyToken(string token)
        {
            var stationId = Guid.NewGuid();

            deactivateValidator.TestValidate(new DeactivateStationCommand(stationId, token))
                .ShouldHaveValidationErrorFor(command => command.ConcurrencyToken);
            reactivateValidator.TestValidate(new ReactivateStationCommand(stationId, token))
                .ShouldHaveValidationErrorFor(command => command.ConcurrencyToken);
        }

        [Fact]
        public void LifecycleCommands_RequireAStation()
        {
            deactivateValidator.TestValidate(new DeactivateStationCommand(Guid.Empty, "1"))
                .ShouldHaveValidationErrorFor(command => command.StationId);
            reactivateValidator.TestValidate(new ReactivateStationCommand(Guid.Empty, "1"))
                .ShouldHaveValidationErrorFor(command => command.StationId);
        }

        [Fact]
        public void Get_RequiresAStation()
        {
            new GetStationQueryValidator()
                .TestValidate(new GetStationQuery(Guid.Empty))
                .ShouldHaveValidationErrorFor(query => query.StationId);
        }
    }
}
