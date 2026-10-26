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
    }
}
