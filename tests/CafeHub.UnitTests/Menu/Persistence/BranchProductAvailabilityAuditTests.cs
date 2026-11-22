using CafeHub.Application.Menu;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class BranchProductAvailabilityAuditTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 22, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid BranchId = Guid.NewGuid();
        private static readonly Guid ProductId = Guid.NewGuid();
        private static readonly Guid Actor = Guid.NewGuid();

        [Fact]
        public void Create_SnapshotsTheBranchAndTheInitialUnavailableState()
        {
            var row = BranchProductAvailability.Create(
                BranchId, ProductId, false, BranchProductAvailabilityReasons.Manual, Now, Actor);

            var changes = TenantAuditChangeSets.BranchProductAvailabilityCreate(row);
            var snapshot = TenantAuditSnapshotSerializer.Create("products", changes).Value;

            Assert.Null(snapshot.BeforeState);
            Assert.Contains($"\"BranchId\":\"{BranchId:D}\"", snapshot.AfterState);
            Assert.Contains("\"IsAvailable\":\"False\"", snapshot.AfterState);
            Assert.Contains("\"ReasonCode\":\"manual\"", snapshot.AfterState);
        }

        [Fact]
        public void Update_RecordsTheOldAndNewAvailability()
        {
            var row = BranchProductAvailability.Create(
                BranchId, ProductId, false, BranchProductAvailabilityReasons.Manual, Now, Actor);
            row.Set(true, BranchProductAvailabilityReasons.Manual, Now.AddMinutes(1), Actor);

            var changes = TenantAuditChangeSets.BranchProductAvailabilityUpdate(false, "manual", row);
            var snapshot = TenantAuditSnapshotSerializer.Create("products", changes).Value;

            Assert.Contains("\"IsAvailable\":\"False\"", snapshot.BeforeState);
            Assert.Contains("\"IsAvailable\":\"True\"", snapshot.AfterState);
            // The reason is unchanged, so the change set keeps only the changed field.
            Assert.DoesNotContain("ReasonCode", snapshot.BeforeState);
        }

        [Fact]
        public void Errors_UseTheFieldNamesTheApiReturns()
        {
            Assert.Equal("productId", Assert.Single(
                Assert.IsType<CafeHub.Application.Abstractions.Results.ValidationError>(
                    BranchProductAvailabilityErrors.ProductNotActive()).Errors).Field);
            Assert.Equal("branchId", Assert.Single(
                Assert.IsType<CafeHub.Application.Abstractions.Results.ValidationError>(
                    BranchProductAvailabilityErrors.BranchNotActive()).Errors).Field);
        }
    }
}
