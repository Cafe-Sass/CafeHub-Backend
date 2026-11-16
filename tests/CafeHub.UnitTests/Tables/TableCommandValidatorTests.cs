using CafeHub.Application.Tables.Commands;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tables
{
    public sealed class TableCommandValidatorTests
    {
        [Theory]
        [InlineData(1, true)]
        [InlineData(9999, true)]
        [InlineData(0, false)]
        [InlineData(10000, false)]
        [InlineData(-1, false)]
        public void AddTable_EnforcesTheNumberRangeInTheDomain(int number, bool accepted)
        {
            if (accepted)
            {
                var table = CreateTable(number);
                Assert.Equal(number, table.Number);
                return;
            }

            Assert.Throws<ArgumentException>(() => CreateTable(number));
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(200, true)]
        [InlineData(0, false)]
        [InlineData(201, false)]
        [InlineData(-5, false)]
        public void AddBulk_EnforcesTheCountRange(int count, bool accepted)
        {
            var validator = new AddTablesBulkCommandValidator();
            var result = validator.Validate(new AddTablesBulkCommand(Guid.NewGuid(), count));
            Assert.Equal(accepted, result.IsValid);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(200, true)]
        [InlineData(0, false)]
        [InlineData(201, false)]
        [InlineData(-5, false)]
        public void DeactivateHighest_EnforcesTheCountRange(int count, bool accepted)
        {
            var validator = new DeactivateHighestTablesCommandValidator();
            var result = validator.Validate(new DeactivateHighestTablesCommand(Guid.NewGuid(), count));
            Assert.Equal(accepted, result.IsValid);
        }

        [Theory]
        [InlineData(64, true)]
        [InlineData(65, false)]
        public void Label_EnforcesTheMaximumLengthAfterTrimming(int length, bool accepted)
        {
            var validator = new AddTableCommandValidator();
            var label = new string('a', length);
            var result = validator.Validate(new AddTableCommand(Guid.NewGuid(), $"  {label}  "));
            Assert.Equal(accepted, result.IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Label_AcceptsANullOrBlankLabel(string? label)
        {
            var validator = new UpdateTableCommandValidator();
            var result = validator.Validate(new UpdateTableCommand(Guid.NewGuid(), label, "7"));
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("Bar\u0000")]
        [InlineData("Kit\u0007chen")]
        public void Label_RejectsControlCharacters(string label)
        {
            var validator = new AddTableCommandValidator();
            var result = validator.Validate(new AddTableCommand(Guid.NewGuid(), label));
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("7", true)]
        [InlineData("0", true)]
        [InlineData("not-a-version", false)]
        [InlineData("", false)]
        public void ConcurrencyToken_MustBeAnUnsignedInteger(string token, bool accepted)
        {
            var validator = new UpdateTableCommandValidator();
            var result = validator.Validate(new UpdateTableCommand(Guid.NewGuid(), null, token));
            Assert.Equal(accepted, result.IsValid);
        }

        [Fact]
        public void TableId_IsRequiredForLifecycleCommands()
        {
            var validator = new DeactivateTableCommandValidator();
            var result = validator.Validate(new DeactivateTableCommand(Guid.Empty, "7"));
            Assert.False(result.IsValid);
        }

        private static BranchTable CreateTable(int number) =>
            BranchTable.Create(
                Guid.NewGuid(), Guid.NewGuid(), number, null, "Abcdefghijklmnopqrstuv", new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero));
    }
}
