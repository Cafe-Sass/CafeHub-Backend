using CafeHub.Domain.Tenants;

namespace CafeHub.Application.CafeProfile.DTOs
{
    /// <summary>
    /// The cafe profile read model returned to a tenant user. It carries only the contract fields:
    /// cafe identity and branding, contact details, time zone, and an opaque concurrency token.
    /// </summary>
    public sealed class CafeProfileResponse
    {
        public required string CafeName { get; init; }

        public required string? LogoMediaReference { get; init; }

        public required string? PrimaryColor { get; init; }

        public required string? SecondaryColor { get; init; }

        public required string? AccentColor { get; init; }

        public required string? ContactPhone { get; init; }

        public required string? ContactEmail { get; init; }

        public required string? ContactAddress { get; init; }

        public required string Timezone { get; init; }

        public required string ConcurrencyToken { get; init; }

        public static CafeProfileResponse From(CafeSettings settings, string concurrencyToken)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(concurrencyToken);

            return new CafeProfileResponse
            {
                CafeName = settings.CafeName,
                LogoMediaReference = settings.LogoMediaReference,
                PrimaryColor = settings.PrimaryColor,
                SecondaryColor = settings.SecondaryColor,
                AccentColor = settings.AccentColor,
                ContactPhone = settings.ContactPhone,
                ContactEmail = settings.ContactEmail,
                ContactAddress = settings.ContactAddress,
                Timezone = settings.Timezone,
                ConcurrencyToken = concurrencyToken,
            };
        }
    }
}
