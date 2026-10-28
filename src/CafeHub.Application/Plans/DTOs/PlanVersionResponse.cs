using CafeHub.Domain.Plans;

namespace CafeHub.Application.Plans.DTOs
{
    public sealed class PlanVersionResponse
    {
        public required Guid PlanVersionId { get; init; }

        public required int Number { get; init; }

        public required decimal Price { get; init; }

        public required string Currency { get; init; }

        public required string Period { get; init; }

        public required IReadOnlyList<PlanEntitlementResponse> Entitlements { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required DateTimeOffset UpdatedAt { get; init; }

        public static PlanVersionResponse From(PlanVersion version)
        {
            ArgumentNullException.ThrowIfNull(version);

            return new PlanVersionResponse
            {
                PlanVersionId = version.Id,
                Number = version.VersionNumber,
                Price = version.Price,
                Currency = version.CurrencyCode,
                Period = version.BillingPeriod.ToString(),
                Entitlements = version.Entitlements
                    .OrderBy(entitlement => entitlement.Key, StringComparer.Ordinal)
                    .Select(entitlement => new PlanEntitlementResponse
                    {
                        Key = entitlement.Key,
                        Value = entitlement.Value,
                    })
                    .ToArray(),
                CreatedAt = version.CreatedAt,
                UpdatedAt = version.UpdatedAt,
            };
        }
    }
}
