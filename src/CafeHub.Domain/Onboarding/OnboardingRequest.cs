using System.Text.RegularExpressions;

namespace CafeHub.Domain.Onboarding
{
    public sealed class OnboardingRequest
    {
        private static readonly Regex TenantSlugPattern = new(
            "^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> ReservedTenantSlugs = new(
            new[]
            {
                "admin", "api", "t", "www", "app", "static", "assets",
                "health", "login", "signup", "support", "platform",
            },
            StringComparer.Ordinal);

        private OnboardingRequest()
        {
        }

        public Guid Id { get; private set; }

        public OnboardingRequestCreatedVia CreatedVia { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid PlanVersionId { get; private set; }

        public string OwnerEmail { get; private set; } = string.Empty;

        public string OwnerFullName { get; private set; } = string.Empty;

        public string? OwnerPhoneNumber { get; private set; }

        public string CafeName { get; private set; } = string.Empty;

        public string? OwnerPasswordHash { get; private set; }

        public string TenantSlug { get; private set; } = string.Empty;

        public OnboardingRequestStatus Status { get; internal set; }

        public Guid? ReviewedBy { get; private set; }

        public DateTimeOffset? ReviewedAt { get; private set; }

        public string? RejectionReason { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static OnboardingRequest CreateForPublicRegistration(
            Guid id,
            Guid planVersionId,
            string ownerEmail,
            string ownerFullName,
            string? ownerPhoneNumber,
            string cafeName,
            string ownerPasswordHash,
            string tenantSlug,
            DateTimeOffset createdAt)
        {
            ValidateCommonInvariants(
                id,
                planVersionId,
                ownerEmail,
                ownerFullName,
                ownerPhoneNumber,
                cafeName,
                tenantSlug);

            if (string.IsNullOrWhiteSpace(ownerPasswordHash) || ownerPasswordHash.Length > 512)
            {
                throw new ArgumentException(
                    "A public registration must carry an owner password hash between 1 and 512 characters.",
                    nameof(ownerPasswordHash));
            }

            return new OnboardingRequest
            {
                Id = id,
                CreatedVia = OnboardingRequestCreatedVia.PublicRegistration,
                CreatedBy = null,
                PlanVersionId = planVersionId,
                OwnerEmail = ownerEmail.ToLowerInvariant(),
                OwnerFullName = ownerFullName,
                OwnerPhoneNumber = ownerPhoneNumber,
                CafeName = cafeName,
                OwnerPasswordHash = ownerPasswordHash,
                TenantSlug = tenantSlug,
                Status = OnboardingRequestStatus.AwaitingPayment,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                UpdatedBy = null,
            };
        }

        public static OnboardingRequest CreateForSupport(
            Guid id,
            Guid planVersionId,
            string ownerEmail,
            string ownerFullName,
            string? ownerPhoneNumber,
            string cafeName,
            string? ownerPasswordHash,
            string tenantSlug,
            DateTimeOffset createdAt,
            Guid createdBy)
        {
            ValidateCommonInvariants(
                id,
                planVersionId,
                ownerEmail,
                ownerFullName,
                ownerPhoneNumber,
                cafeName,
                tenantSlug);

            if (createdBy == Guid.Empty)
            {
                throw new ArgumentException("The support actor is required.", nameof(createdBy));
            }

            if (ownerPasswordHash is not null
                && (string.IsNullOrWhiteSpace(ownerPasswordHash) || ownerPasswordHash.Length > 512))
            {
                throw new ArgumentException(
                    "An owner password hash, when present, must be between 1 and 512 characters.",
                    nameof(ownerPasswordHash));
            }

            return new OnboardingRequest
            {
                Id = id,
                CreatedVia = OnboardingRequestCreatedVia.Support,
                CreatedBy = createdBy,
                PlanVersionId = planVersionId,
                OwnerEmail = ownerEmail,
                OwnerFullName = ownerFullName,
                OwnerPhoneNumber = ownerPhoneNumber,
                CafeName = cafeName,
                OwnerPasswordHash = ownerPasswordHash,
                TenantSlug = tenantSlug,
                Status = OnboardingRequestStatus.AwaitingPayment,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                UpdatedBy = createdBy,
            };
        }

        public void TransitionTo(OnboardingRequestStatus newStatus, DateTimeOffset now)
        {
            if (!Enum.IsDefined(newStatus))
            {
                throw new ArgumentException("The onboarding request status is not defined.", nameof(newStatus));
            }

            Status = newStatus;
            UpdatedAt = now;
            UpdatedBy = null;
        }

        public void Approve(Guid reviewedBy, DateTimeOffset reviewedAt)
        {
            if (reviewedBy == Guid.Empty)
            {
                throw new ArgumentException("The reviewing administrator is required.", nameof(reviewedBy));
            }

            Status = OnboardingRequestStatus.Approved;
            ReviewedBy = reviewedBy;
            ReviewedAt = reviewedAt;
            UpdatedAt = reviewedAt;
            UpdatedBy = reviewedBy;
        }

        public void Reject(Guid reviewedBy, DateTimeOffset reviewedAt, string? rejectionReason)
        {
            if (reviewedBy == Guid.Empty)
            {
                throw new ArgumentException("The reviewing administrator is required.", nameof(reviewedBy));
            }

            if (rejectionReason is not null && rejectionReason.Length > 1024)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rejectionReason),
                    "The rejection reason must not exceed 1024 characters.");
            }

            Status = OnboardingRequestStatus.Rejected;
            ReviewedBy = reviewedBy;
            ReviewedAt = reviewedAt;
            RejectionReason = rejectionReason;
            UpdatedAt = reviewedAt;
            UpdatedBy = reviewedBy;
        }

        private static void ValidateCommonInvariants(
            Guid id,
            Guid planVersionId,
            string ownerEmail,
            string ownerFullName,
            string? ownerPhoneNumber,
            string cafeName,
            string tenantSlug)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The onboarding request id is required.", nameof(id));
            }

            if (planVersionId == Guid.Empty)
            {
                throw new ArgumentException("The plan version id is required.", nameof(planVersionId));
            }

            if (string.IsNullOrWhiteSpace(ownerEmail) || ownerEmail.Length > 320)
            {
                throw new ArgumentException(
                    "The owner email must be between 1 and 320 characters.",
                    nameof(ownerEmail));
            }

            if (string.IsNullOrWhiteSpace(ownerFullName) || ownerFullName.Length > 256)
            {
                throw new ArgumentException(
                    "The owner full name must be between 1 and 256 characters.",
                    nameof(ownerFullName));
            }

            if (ownerPhoneNumber is not null
                && (string.IsNullOrWhiteSpace(ownerPhoneNumber) || ownerPhoneNumber.Length > 64))
            {
                throw new ArgumentException(
                    "The owner phone number, when present, must be between 1 and 64 characters.",
                    nameof(ownerPhoneNumber));
            }

            if (string.IsNullOrWhiteSpace(cafeName) || cafeName.Length > 256)
            {
                throw new ArgumentException(
                    "The cafe name must be between 1 and 256 characters.",
                    nameof(cafeName));
            }

            if (!IsWellFormedTenantSlug(tenantSlug))
            {
                throw new ArgumentException(
                    "The tenant slug must be 3 to 40 characters of lowercase letters and digits with optional internal hyphens, and must not be a reserved word.",
                    nameof(tenantSlug));
            }
        }

        private static bool IsWellFormedTenantSlug(string? tenantSlug)
        {
            if (tenantSlug is null || !TenantSlugPattern.IsMatch(tenantSlug))
            {
                return false;
            }

            return !ReservedTenantSlugs.Contains(tenantSlug);
        }
    }
}
