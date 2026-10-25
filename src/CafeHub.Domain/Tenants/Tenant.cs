using System.Text.RegularExpressions;

namespace CafeHub.Domain.Tenants
{
    public sealed class Tenant
    {
        private static readonly Regex SlugPattern = new(
            "^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> ReservedSlugs = new(
            new[]
            {
                "admin", "api", "t", "www", "app", "static", "assets",
                "health", "login", "signup", "support", "platform",
            },
            StringComparer.Ordinal);

        private Tenant()
        {
        }

        public Guid Id { get; private set; }

        public Guid OnboardingRequestId { get; private set; }

        public string Slug { get; private set; } = string.Empty;

        public TenantLifecycleStatus LifecycleStatus { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static Tenant Create(
            Guid id,
            Guid onboardingRequestId,
            string slug,
            TenantLifecycleStatus lifecycleStatus,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant id is required.", nameof(id));
            }

            if (onboardingRequestId == Guid.Empty)
            {
                throw new ArgumentException("The onboarding request id is required.", nameof(onboardingRequestId));
            }

            if (!IsWellFormedSlug(slug))
            {
                throw new ArgumentException(
                    "The tenant slug must be 3 to 40 characters of lowercase letters and digits with optional internal hyphens, and must not be a reserved word.",
                    nameof(slug));
            }

            if (!Enum.IsDefined(lifecycleStatus))
            {
                throw new ArgumentException("The tenant lifecycle status is not defined.", nameof(lifecycleStatus));
            }

            return new Tenant
            {
                Id = id,
                OnboardingRequestId = onboardingRequestId,
                Slug = slug,
                LifecycleStatus = lifecycleStatus,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        public void TransitionTo(TenantLifecycleStatus newStatus, DateTimeOffset now)
        {
            if (!Enum.IsDefined(newStatus))
            {
                throw new ArgumentException("The tenant lifecycle status is not defined.", nameof(newStatus));
            }

            LifecycleStatus = newStatus;
            UpdatedAt = now;
        }

        private static bool IsWellFormedSlug(string? slug)
        {
            if (slug is null || !SlugPattern.IsMatch(slug))
            {
                return false;
            }

            return !ReservedSlugs.Contains(slug);
        }
    }
}
