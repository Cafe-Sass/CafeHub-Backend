using CafeHub.Domain.Onboarding;

namespace CafeHub.Application.Onboarding.DTOs
{
    /// <summary>
    /// Review queue list projection. Deliberately excludes <see cref="OnboardingRequest.OwnerPasswordHash"/>,
    /// idempotency keys, and payment provider references.
    /// </summary>
    public sealed class OnboardingRequestListItem
    {
        public required Guid RequestId { get; init; }

        public required string State { get; init; }

        public required string OwnerEmail { get; init; }

        public required string OwnerFullName { get; init; }

        public required string CafeName { get; init; }

        public required string TenantSlug { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public static OnboardingRequestListItem From(OnboardingRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new OnboardingRequestListItem
            {
                RequestId = request.Id,
                State = request.Status.ToString(),
                OwnerEmail = request.OwnerEmail,
                OwnerFullName = request.OwnerFullName,
                CafeName = request.CafeName,
                TenantSlug = request.TenantSlug,
                CreatedAt = request.CreatedAt,
            };
        }
    }
}
