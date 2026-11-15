using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tables
{
    public sealed class BranchTableTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 15, 0, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Create_TrimsTheLabelAndStartsActive()
        {
            var table = BranchTable.Create(
                Guid.NewGuid(),
                Guid.NewGuid(),
                3,
                "  Terrace 3  ",
                "Abcdefghijklmnopqrstuv",
                Now);

            Assert.Equal("Terrace 3", table.Label);
            Assert.Equal(BranchTableStatus.Active, table.Status);
            Assert.Equal(3, table.Number);
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(9999, true)]
        [InlineData(0, false)]
        [InlineData(10000, false)]
        [InlineData(-1, false)]
        public void Create_EnforcesTheNumberRange(int number, bool accepted)
        {
            if (accepted)
            {
                Assert.Equal(number, BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), number, null, "Abcdefghijklmnopqrstuv", Now).Number);
                return;
            }

            Assert.Throws<ArgumentException>(() => BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), number, null, "Abcdefghijklmnopqrstuv", Now));
        }

        [Theory]
        [InlineData(64, true)]
        [InlineData(65, false)]
        public void Create_EnforcesTheLabelLengthAfterTrimming(int length, bool accepted)
        {
            var label = new string('a', length);

            if (accepted)
            {
                Assert.Equal(label, BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), 1, $"  {label}  ", "Abcdefghijklmnopqrstuv", Now).Label);
                return;
            }

            Assert.Throws<ArgumentException>(() => BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), 1, label, "Abcdefghijklmnopqrstuv", Now));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_AcceptsANullOrBlankLabelAndStoresNull(string? label)
        {
            Assert.Null(BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), 1, label, "Abcdefghijklmnopqrstuv", Now).Label);
        }

        [Theory]
        [InlineData("Bar\u0000")]
        [InlineData("Kit\u0007chen")]
        public void Create_RejectsControlCharactersInTheLabel(string label)
        {
            Assert.Throws<ArgumentException>(() => BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), 1, label, "Abcdefghijklmnopqrstuv", Now));
        }

        [Fact]
        public void Create_RejectsAnEmptyId()
        {
            Assert.Throws<ArgumentException>(() => BranchTable.Create(Guid.Empty, Guid.NewGuid(), 1, null, "Abcdefghijklmnopqrstuv", Now));
        }

        [Fact]
        public void Create_RejectsAnEmptyBranchId()
        {
            Assert.Throws<ArgumentException>(() => BranchTable.Create(Guid.NewGuid(), Guid.Empty, 1, null, "Abcdefghijklmnopqrstuv", Now));
        }

        [Theory]
        [InlineData("Abcdefghijklmnopqrstuv+")]
        [InlineData("Abcdefghijklmnopqrstuv/")]
        [InlineData("Abcdefghijklmnopqrstuv ")]
        public void Create_RejectsAnInvalidQrCode(string qrCode)
        {
            Assert.Throws<ArgumentException>(() => BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), 1, null, qrCode, Now));
        }

        [Fact]
        public void Create_AcceptsAValidUrlSafeQrCode()
        {
            var table = BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), 1, null, "Abcdefghijklmnopqrstuv", Now);

            Assert.Equal("Abcdefghijklmnopqrstuv", table.QrCode);
        }

        [Fact]
        public void UpdateLabel_ChangesOnlyTheLabelAndTimestamps()
        {
            var table = BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), 1, null, "Abcdefghijklmnopqrstuv", Now);
            var actor = Guid.NewGuid();
            var later = Now.AddMinutes(5);

            table.UpdateLabel("  Counter 7  ", later, actor);

            Assert.Equal("Counter 7", table.Label);
            Assert.Equal(later, table.UpdatedAt);
            Assert.Equal(actor, table.UpdatedBy);
            Assert.Equal(BranchTableStatus.Active, table.Status);
        }

        [Fact]
        public void DeactivateAndReactivate_ChangeOnlyTheStatusAndKeepTheRow()
        {
            var table = BranchTable.Create(Guid.NewGuid(), Guid.NewGuid(), 1, null, "Abcdefghijklmnopqrstuv", Now);
            var actor = Guid.NewGuid();
            var later = Now.AddMinutes(5);

            table.Deactivate(later, actor);

            Assert.Equal(BranchTableStatus.Inactive, table.Status);
            Assert.Equal(later, table.UpdatedAt);
            Assert.Equal(actor, table.UpdatedBy);
            Assert.Equal("Abcdefghijklmnopqrstuv", table.QrCode);

            table.Reactivate(later.AddMinutes(5), actor);

            Assert.Equal(BranchTableStatus.Active, table.Status);
            Assert.Equal(1, table.Number);
        }
    }
}
