using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class CafeSettingsTests
    {
        private static readonly Guid Id = Guid.CreateVersion7();
        private static readonly Guid ActorId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var settings = CafeSettings.Create(
                Id,
                "My Cafe",
                "media-ref-123",
                "#ff0000",
                "#00ff00",
                "#0000ff",
                "+1-555-0100",
                "contact@example.com",
                "123 Main St",
                "America/New_York",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ActorId);

            Assert.Equal(Id, settings.Id);
            Assert.Equal("My Cafe", settings.CafeName);
            Assert.Equal("media-ref-123", settings.LogoMediaReference);
            Assert.Equal("#ff0000", settings.PrimaryColor);
            Assert.Equal("#00ff00", settings.SecondaryColor);
            Assert.Equal("#0000ff", settings.AccentColor);
            Assert.Equal("+1-555-0100", settings.ContactPhone);
            Assert.Equal("contact@example.com", settings.ContactEmail);
            Assert.Equal("123 Main St", settings.ContactAddress);
            Assert.Equal("America/New_York", settings.Timezone);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), settings.CreatedAt);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), settings.UpdatedAt);
            Assert.Equal(ActorId, settings.CreatedBy);
            Assert.Equal(ActorId, settings.UpdatedBy);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                CafeSettings.Create(
                    Guid.Empty, "My Cafe", null, null, null, null, null, null, null,
                    "America/New_York", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptyCafeName()
        {
            Assert.Throws<ArgumentException>(() =>
                CafeSettings.Create(
                    Id, "", null, null, null, null, null, null, null,
                    "America/New_York", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsInvalidColor()
        {
            Assert.Throws<ArgumentException>(() =>
                CafeSettings.Create(
                    Id, "My Cafe", null, "red", null, null, null, null, null,
                    "America/New_York", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_AllowsNullOptionalFields()
        {
            var settings = CafeSettings.Create(
                Id, "My Cafe", null, null, null, null, null, null, null,
                "America/New_York", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Null(settings.LogoMediaReference);
            Assert.Null(settings.PrimaryColor);
            Assert.Null(settings.SecondaryColor);
            Assert.Null(settings.AccentColor);
            Assert.Null(settings.ContactPhone);
            Assert.Null(settings.ContactEmail);
            Assert.Null(settings.ContactAddress);
        }

        [Fact]
        public void Create_RejectsEmptyTimezone()
        {
            Assert.Throws<ArgumentException>(() =>
                CafeSettings.Create(
                    Id, "My Cafe", null, null, null, null, null, null, null,
                    "", new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }
    }
}
