using System.Reflection;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.CafeProfile
{
    public sealed class CafeProfileResponseTests
    {
        private static readonly Guid Id = Guid.CreateVersion7();
        private static readonly Guid ActorId = Guid.CreateVersion7();

        [Fact]
        public void From_MapsEveryField_IncludingTokenString()
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

            var response = CafeProfileResponse.From(settings, "000007d1");

            Assert.Equal("My Cafe", response.CafeName);
            Assert.Equal("media-ref-123", response.LogoMediaReference);
            Assert.Equal("#ff0000", response.PrimaryColor);
            Assert.Equal("#00ff00", response.SecondaryColor);
            Assert.Equal("#0000ff", response.AccentColor);
            Assert.Equal("+1-555-0100", response.ContactPhone);
            Assert.Equal("contact@example.com", response.ContactEmail);
            Assert.Equal("123 Main St", response.ContactAddress);
            Assert.Equal("America/New_York", response.Timezone);
            Assert.Equal("000007d1", response.ConcurrencyToken);
        }

        [Fact]
        public void From_NullOptionalFieldsStayNull()
        {
            var settings = CafeSettings.Create(
                Id,
                "My Cafe",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "America/New_York",
                new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ActorId);

            var response = CafeProfileResponse.From(settings, "0");

            Assert.Null(response.LogoMediaReference);
            Assert.Null(response.PrimaryColor);
            Assert.Null(response.SecondaryColor);
            Assert.Null(response.AccentColor);
            Assert.Null(response.ContactPhone);
            Assert.Null(response.ContactEmail);
            Assert.Null(response.ContactAddress);
        }

        [Fact]
        public void Response_ExposesExactlyTheContractMembers()
        {
            var type = typeof(CafeProfileResponse);
            var propertyNames = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToHashSet(StringComparer.Ordinal);

            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "CafeName",
                "LogoMediaReference",
                "PrimaryColor",
                "SecondaryColor",
                "AccentColor",
                "ContactPhone",
                "ContactEmail",
                "ContactAddress",
                "Timezone",
                "ConcurrencyToken",
            };

            Assert.Equal(expected, propertyNames);
        }
    }
}
