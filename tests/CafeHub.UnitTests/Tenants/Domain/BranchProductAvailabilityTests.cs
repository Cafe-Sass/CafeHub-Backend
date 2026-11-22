using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class BranchProductAvailabilityTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid BranchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ProductId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid ActorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        [Fact]
        public void Create_RecordsTheUnavailableRowWithAuditColumns()
        {
            var row = BranchProductAvailability.Create(
                BranchId, ProductId, false, BranchProductAvailabilityReasons.Manual, Now, ActorId);

            Assert.Equal(BranchId, row.BranchId);
            Assert.Equal(ProductId, row.ProductId);
            Assert.False(row.IsAvailable);
            Assert.Equal("manual", row.ReasonCode);
            Assert.Equal(Now, row.CreatedAt);
            Assert.Equal(Now, row.UpdatedAt);
            Assert.Equal(ActorId, row.CreatedBy);
            Assert.Equal(ActorId, row.UpdatedBy);
        }

        [Fact]
        public void Set_WithTheSameStateIsANoOpSoTheUpsertStaysIdempotent()
        {
            var row = BranchProductAvailability.Create(
                BranchId, ProductId, false, BranchProductAvailabilityReasons.Manual, Now, ActorId);
            var later = Now.AddMinutes(5);
            var otherActor = Guid.NewGuid();

            Assert.False(row.Set(false, BranchProductAvailabilityReasons.Manual, later, otherActor));

            Assert.Equal(Now, row.UpdatedAt);
            Assert.Equal(ActorId, row.UpdatedBy);
        }

        [Fact]
        public void Set_WithAChangedStateUpdatesTheRowAndAuditColumns()
        {
            var row = BranchProductAvailability.Create(
                BranchId, ProductId, false, BranchProductAvailabilityReasons.Manual, Now, ActorId);
            var later = Now.AddMinutes(5);
            var otherActor = Guid.NewGuid();

            Assert.True(row.Set(true, BranchProductAvailabilityReasons.Manual, later, otherActor));

            Assert.True(row.IsAvailable);
            Assert.Equal(later, row.UpdatedAt);
            Assert.Equal(otherActor, row.UpdatedBy);
            Assert.Equal(Now, row.CreatedAt);
            Assert.Equal(ActorId, row.CreatedBy);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("stock")]
        public void Create_RejectsAReasonOutsideTheClosedList(string? reason)
        {
            Assert.Throws<ArgumentException>(() => BranchProductAvailability.Create(
                BranchId, ProductId, false, reason!, Now, ActorId));
        }

        [Fact]
        public void Reasons_ExposeTheClosedList()
        {
            Assert.Equal(new[] { "manual", "out_of_stock", "expired" }, BranchProductAvailabilityReasons.All);
            Assert.True(BranchProductAvailabilityReasons.IsValid(BranchProductAvailabilityReasons.OutOfStock));
            Assert.False(BranchProductAvailabilityReasons.IsValid("stock"));
        }
    }
}
