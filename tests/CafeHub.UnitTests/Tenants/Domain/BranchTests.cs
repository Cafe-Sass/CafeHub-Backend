using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class BranchTests
    {
        private static readonly Guid Id = Guid.CreateVersion7();
        private static readonly Guid ActorId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var branch = Branch.Create(
                Id,
                "Downtown Cafe",
                BranchLifecycleStatus.Active,
                "123 Main St",
                "+1-555-0100",
                "Mon-Fri 8am-6pm",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ActorId);

            Assert.Equal(Id, branch.Id);
            Assert.Equal("Downtown Cafe", branch.Name);
            Assert.Equal(BranchLifecycleStatus.Active, branch.LifecycleStatus);
            Assert.Equal("123 Main St", branch.Address);
            Assert.Equal("+1-555-0100", branch.ContactPhone);
            Assert.Equal("Mon-Fri 8am-6pm", branch.OpeningHoursSummary);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), branch.CreatedAt);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), branch.UpdatedAt);
            Assert.Equal(ActorId, branch.CreatedBy);
            Assert.Equal(ActorId, branch.UpdatedBy);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                Branch.Create(
                    Guid.Empty, "Downtown Cafe", BranchLifecycleStatus.Active,
                    null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptyName()
        {
            Assert.Throws<ArgumentException>(() =>
                Branch.Create(
                    Id, "", BranchLifecycleStatus.Active,
                    null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsUndefinedLifecycleStatus()
        {
            Assert.Throws<ArgumentException>(() =>
                Branch.Create(
                    Id, "Downtown Cafe", (BranchLifecycleStatus)99,
                    null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_AllowsNullOptionalFields()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Null(branch.Address);
            Assert.Null(branch.ContactPhone);
            Assert.Null(branch.OpeningHoursSummary);
        }

        [Fact]
        public void Create_AcceptsSingleCharacterName()
        {
            var branch = Branch.Create(
                Id, "a", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Equal("a", branch.Name);
        }

        [Fact]
        public void Update_ChangesOnlyEditableFieldsAndLeavesLifecycleStatusUnchanged()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                "123 Main St", "+1-555-0100", "Mon-Fri 8am-6pm",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), ActorId);

            branch.Update(
                "Downtown Cafe West",
                "456 Side St",
                "+1-555-0101",
                "Mon-Fri 9am-7pm",
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId);

            Assert.Equal("Downtown Cafe West", branch.Name);
            Assert.Equal("456 Side St", branch.Address);
            Assert.Equal("+1-555-0101", branch.ContactPhone);
            Assert.Equal("Mon-Fri 9am-7pm", branch.OpeningHoursSummary);
            Assert.Equal(BranchLifecycleStatus.Active, branch.LifecycleStatus);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero), branch.UpdatedAt);
            Assert.Equal(ActorId, branch.UpdatedBy);
        }

        [Fact]
        public void Update_AssignsEditableFieldsAsProvided()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                "123 Main St", "+1-555-0100", "Mon-Fri 8am-6pm",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), ActorId);

            branch.Update(
                "Downtown Cafe West",
                "   ",
                null,
                "",
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId);

            Assert.Equal("Downtown Cafe West", branch.Name);
            Assert.Equal("   ", branch.Address);
            Assert.Null(branch.ContactPhone);
            Assert.Equal("", branch.OpeningHoursSummary);
            Assert.Equal(BranchLifecycleStatus.Active, branch.LifecycleStatus);
        }

        [Fact]
        public void Update_RejectsEmptyName()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() => branch.Update(
                "   ",
                null,
                null,
                null,
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId));
        }

        [Fact]
        public void Update_RejectsNameAboveMaximumLength()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() => branch.Update(
                new string('a', 257),
                null,
                null,
                null,
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId));
        }

        [Fact]
        public void Update_RejectsAddressAboveMaximumLength()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() => branch.Update(
                "Downtown Cafe West",
                new string('a', 513),
                null,
                null,
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId));
        }

        [Fact]
        public void Update_RejectsContactPhoneAboveMaximumLength()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() => branch.Update(
                "Downtown Cafe West",
                null,
                new string('a', 33),
                null,
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId));
        }

        [Fact]
        public void Update_RejectsOpeningHoursSummaryAboveMaximumLength()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() => branch.Update(
                "Downtown Cafe West",
                null,
                null,
                new string('a', 513),
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId));
        }

        [Fact]
        public void ChangeLifecycleStatus_ChangesStatusAndTimestamps()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), ActorId);

            branch.ChangeLifecycleStatus(
                BranchLifecycleStatus.Suspended,
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId);

            Assert.Equal(BranchLifecycleStatus.Suspended, branch.LifecycleStatus);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero), branch.UpdatedAt);
            Assert.Equal(ActorId, branch.UpdatedBy);
        }

        [Fact]
        public void ChangeLifecycleStatus_RejectsUndefinedStatus()
        {
            var branch = Branch.Create(
                Id, "Downtown Cafe", BranchLifecycleStatus.Active,
                null, null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() => branch.ChangeLifecycleStatus(
                (BranchLifecycleStatus)99,
                new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero),
                ActorId));
        }
    }
}
