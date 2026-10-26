using System.Text.RegularExpressions;

namespace CafeHub.Domain.Tenants
{
    public sealed class CafeSettings
    {
        private static readonly Regex ColorPattern = new(
            "^#[0-9a-fA-F]{6}$",
            RegexOptions.CultureInvariant);

        private CafeSettings()
        {
        }

        public Guid Id { get; private set; }

        public string CafeName { get; private set; } = string.Empty;

        public string? LogoMediaReference { get; private set; }

        public string? PrimaryColor { get; private set; }

        public string? SecondaryColor { get; private set; }

        public string? AccentColor { get; private set; }

        public string? ContactPhone { get; private set; }

        public string? ContactEmail { get; private set; }

        public string? ContactAddress { get; private set; }

        public string Timezone { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static CafeSettings Create(
            Guid id,
            string cafeName,
            string? logoMediaReference,
            string? primaryColor,
            string? secondaryColor,
            string? accentColor,
            string? contactPhone,
            string? contactEmail,
            string? contactAddress,
            string timezone,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The cafe settings id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(cafeName) || cafeName.Length > 256)
            {
                throw new ArgumentException(
                    "The cafe name must be between 1 and 256 characters.",
                    nameof(cafeName));
            }

            if (logoMediaReference is not null && logoMediaReference.Length > 512)
            {
                throw new ArgumentException(
                    "The logo media reference must not exceed 512 characters.",
                    nameof(logoMediaReference));
            }

            if (primaryColor is not null && !IsWellFormedColor(primaryColor))
            {
                throw new ArgumentException(
                    "The primary color must be a six-digit hex color code prefixed with '#'.",
                    nameof(primaryColor));
            }

            if (secondaryColor is not null && !IsWellFormedColor(secondaryColor))
            {
                throw new ArgumentException(
                    "The secondary color must be a six-digit hex color code prefixed with '#'.",
                    nameof(secondaryColor));
            }

            if (accentColor is not null && !IsWellFormedColor(accentColor))
            {
                throw new ArgumentException(
                    "The accent color must be a six-digit hex color code prefixed with '#'.",
                    nameof(accentColor));
            }

            if (contactPhone is not null && contactPhone.Length > 32)
            {
                throw new ArgumentException(
                    "The contact phone must not exceed 32 characters.",
                    nameof(contactPhone));
            }

            if (contactEmail is not null && contactEmail.Length > 320)
            {
                throw new ArgumentException(
                    "The contact email must not exceed 320 characters.",
                    nameof(contactEmail));
            }

            if (contactAddress is not null && contactAddress.Length > 512)
            {
                throw new ArgumentException(
                    "The contact address must not exceed 512 characters.",
                    nameof(contactAddress));
            }

            if (string.IsNullOrWhiteSpace(timezone) || timezone.Length > 64)
            {
                throw new ArgumentException(
                    "The timezone must be between 1 and 64 characters.",
                    nameof(timezone));
            }

            return new CafeSettings
            {
                Id = id,
                CafeName = cafeName,
                LogoMediaReference = logoMediaReference,
                PrimaryColor = primaryColor,
                SecondaryColor = secondaryColor,
                AccentColor = accentColor,
                ContactPhone = contactPhone,
                ContactEmail = contactEmail,
                ContactAddress = contactAddress,
                Timezone = timezone,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        private static bool IsWellFormedColor(string? color)
        {
            if (color is null)
            {
                return false;
            }

            return ColorPattern.IsMatch(color);
        }
    }
}
