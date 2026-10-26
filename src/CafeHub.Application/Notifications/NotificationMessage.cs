namespace CafeHub.Application.Notifications
{
    public sealed class NotificationMessage
    {
        private static readonly HashSet<string> DeniedValueKeys = new(
            new[]
            {
                "password",
                "hash",
                "token",
                "secret",
                "key",
            },
            StringComparer.OrdinalIgnoreCase);

        public NotificationMessage(
            NotificationKind kind,
            Guid tenantId,
            string recipientEmail,
            string templateKey,
            IReadOnlyDictionary<string, string> values)
        {
            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentException("The notification kind is not defined.", nameof(kind));
            }

            if (tenantId == Guid.Empty)
            {
                throw new ArgumentException("The tenant id is required.", nameof(tenantId));
            }

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new ArgumentException("The recipient email is required.", nameof(recipientEmail));
            }

            if (string.IsNullOrWhiteSpace(templateKey))
            {
                throw new ArgumentException("The template key is required.", nameof(templateKey));
            }

            ArgumentNullException.ThrowIfNull(values);

            if (values.Keys.Any(DeniedValueKeys.Contains))
            {
                throw new ArgumentException("The notification values contain a denied key name.", nameof(values));
            }

            Kind = kind;
            TenantId = tenantId;
            RecipientEmail = recipientEmail;
            TemplateKey = templateKey;
            Values = values;
        }

        public NotificationKind Kind { get; }

        public Guid TenantId { get; }

        public string RecipientEmail { get; }

        public string TemplateKey { get; }

        public IReadOnlyDictionary<string, string> Values { get; }

        public static NotificationMessage TenantReady(
            Guid tenantId,
            string recipientEmail,
            string cafeName,
            string tenantSlug)
        {
            return new NotificationMessage(
                NotificationKind.TenantReady,
                tenantId,
                recipientEmail,
                NotificationTemplateKeys.TenantReady,
                new Dictionary<string, string>
                {
                    ["cafeName"] = cafeName,
                    ["tenantSlug"] = tenantSlug,
                });
        }
    }
}
