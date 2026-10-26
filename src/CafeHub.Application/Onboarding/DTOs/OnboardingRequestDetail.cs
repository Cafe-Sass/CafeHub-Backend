using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Plans;
using CafeHub.Domain.Subscriptions;

namespace CafeHub.Application.Onboarding.DTOs
{
    /// <summary>
    /// Single onboarding request detail projection. Deliberately excludes
    /// <see cref="OnboardingRequest.OwnerPasswordHash"/>, idempotency keys, and payment provider references.
    /// </summary>
    public sealed class OnboardingRequestDetail
    {
        public required Guid RequestId { get; init; }

        public required string State { get; init; }

        public required string OwnerEmail { get; init; }

        public required string OwnerFullName { get; init; }

        public required string? OwnerPhoneNumber { get; init; }

        public required string CafeName { get; init; }

        public required string TenantSlug { get; init; }

        public required string PlanKey { get; init; }

        public required int PlanVersionNumber { get; init; }

        public required string? PaymentStatus { get; init; }

        public required DateTimeOffset? PaidAt { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }

        public required Guid? ReviewedBy { get; init; }

        public required DateTimeOffset? ReviewedAt { get; init; }

        public required string? RejectionReason { get; init; }

        public static OnboardingRequestDetail From(
            OnboardingRequest request,
            PlanVersion? planVersion,
            Plan? plan,
            PlatformPaymentTransaction? payment)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new OnboardingRequestDetail
            {
                RequestId = request.Id,
                State = request.Status.ToString(),
                OwnerEmail = request.OwnerEmail,
                OwnerFullName = request.OwnerFullName,
                OwnerPhoneNumber = request.OwnerPhoneNumber,
                CafeName = request.CafeName,
                TenantSlug = request.TenantSlug,
                PlanKey = plan?.Code ?? string.Empty,
                PlanVersionNumber = planVersion?.VersionNumber ?? 0,
                PaymentStatus = payment?.Status.ToString(),
                PaidAt = payment?.SettledAt,
                CreatedAt = request.CreatedAt,
                ReviewedBy = request.ReviewedBy,
                ReviewedAt = request.ReviewedAt,
                RejectionReason = request.RejectionReason,
            };
        }
    }
}
