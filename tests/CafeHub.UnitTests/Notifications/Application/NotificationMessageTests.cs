using CafeHub.Application.Notifications;

namespace CafeHub.UnitTests.Notifications.Application
{
    public sealed class NotificationMessageTests
    {
        private static readonly Dictionary<string, string> AllowedValues = new()
        {
            ["cafeName"] = "Cafe Name",
            ["tenantSlug"] = "my-cafe",
        };

        public static TheoryData<string> DeniedKeyNames => new()
        {
            "password",
            "hash",
            "token",
            "secret",
            "key",
            "Password",
            "TOKEN",
        };

        [Theory]
        [MemberData(nameof(DeniedKeyNames))]
        public void Message_RejectsDeniedValueKeyNames(string deniedKey)
        {
            var values = new Dictionary<string, string> { [deniedKey] = "value" };

            Assert.Throws<ArgumentException>(() => new NotificationMessage(
                NotificationKind.TenantReady,
                Guid.CreateVersion7(),
                "owner@example.com",
                NotificationTemplateKeys.TenantReady,
                values));
        }

        [Fact]
        public void TenantReadyMessage_ContainsOnlyCafeNameAndSlug()
        {
            var message = NotificationMessage.TenantReady(
                Guid.CreateVersion7(),
                "owner@example.com",
                "Cafe Name",
                "my-cafe");

            Assert.Equal(new[] { "cafeName", "tenantSlug" }, message.Values.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Equal("Cafe Name", message.Values["cafeName"]);
            Assert.Equal("my-cafe", message.Values["tenantSlug"]);
            Assert.Equal(NotificationKind.TenantReady, message.Kind);
            Assert.Equal(NotificationTemplateKeys.TenantReady, message.TemplateKey);
        }

        [Fact]
        public void Message_RejectsUndefinedKind()
        {
            Assert.Throws<ArgumentException>(() => new NotificationMessage(
                (NotificationKind)999,
                Guid.CreateVersion7(),
                "owner@example.com",
                NotificationTemplateKeys.TenantReady,
                AllowedValues));
        }

        [Fact]
        public void Message_RejectsEmptyTenantId()
        {
            Assert.Throws<ArgumentException>(() => new NotificationMessage(
                NotificationKind.TenantReady,
                Guid.Empty,
                "owner@example.com",
                NotificationTemplateKeys.TenantReady,
                AllowedValues));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Message_RejectsMissingRecipientEmail(string? recipientEmail)
        {
            Assert.Throws<ArgumentException>(() => new NotificationMessage(
                NotificationKind.TenantReady,
                Guid.CreateVersion7(),
                recipientEmail!,
                NotificationTemplateKeys.TenantReady,
                AllowedValues));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Message_RejectsMissingTemplateKey(string? templateKey)
        {
            Assert.Throws<ArgumentException>(() => new NotificationMessage(
                NotificationKind.TenantReady,
                Guid.CreateVersion7(),
                "owner@example.com",
                templateKey!,
                AllowedValues));
        }
    }
}
