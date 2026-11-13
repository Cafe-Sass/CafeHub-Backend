using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

namespace CafeHub.UnitTests.CafeProfile.Persistence
{
    public sealed class CafeSettingsWriterSnapshotTests
    {
        [Fact]
        public void CafeProfileUpdate_CapturesOnlyApprovedOldAndNewFields()
        {
            var settings = CafeSettings.Create(
                Guid.NewGuid(),
                "Old Cafe",
                "excluded-logo",
                "#111111",
                "#222222",
                "#333333",
                "excluded-phone",
                "excluded-email",
                "excluded-address",
                "UTC",
                DateTimeOffset.UtcNow);

            var changes = TenantAuditChangeSets.CafeProfileUpdate(
                settings,
                "New Cafe",
                "#aaaaaa",
                "#bbbbbb",
                "#cccccc",
                "Africa/Cairo");

            Assert.False(changes.IsCreate);
            Assert.Equal(
                new[] { "CafeName", "PrimaryColor", "SecondaryColor", "AccentColor", "Timezone" },
                changes.Fields.Select(field => field.Name).ToArray());
            Assert.Equal("Old Cafe", changes.Fields[0].Before);
            Assert.Equal("New Cafe", changes.Fields[0].After);
            Assert.Equal("UTC", changes.Fields[^1].Before);
            Assert.Equal("Africa/Cairo", changes.Fields[^1].After);
            Assert.DoesNotContain(changes.Fields, field => field.Name.Contains("Email", StringComparison.Ordinal));
            Assert.DoesNotContain(changes.Fields, field => field.Name.Contains("Phone", StringComparison.Ordinal));
            Assert.DoesNotContain(changes.Fields, field => field.Name.Contains("Address", StringComparison.Ordinal));
            Assert.DoesNotContain(changes.Fields, field => field.Name.Contains("Logo", StringComparison.Ordinal));
        }

        [Fact]
        public void CafeProfileUpdate_WithSameApprovedValuesProducesNoSnapshotPayload()
        {
            var settings = CafeSettings.Create(
                Guid.NewGuid(),
                "Cafe",
                null,
                "#111111",
                "#222222",
                "#333333",
                null,
                null,
                null,
                "UTC",
                DateTimeOffset.UtcNow);

            var changes = TenantAuditChangeSets.CafeProfileUpdate(
                settings,
                settings.CafeName,
                settings.PrimaryColor,
                settings.SecondaryColor,
                settings.AccentColor,
                settings.Timezone);
            var result = TenantAuditSnapshotSerializer.Create("cafe_settings", changes);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.BeforeState);
            Assert.Null(result.Value.AfterState);
        }
    }
}
