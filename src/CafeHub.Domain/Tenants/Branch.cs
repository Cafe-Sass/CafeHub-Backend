using System.Text.RegularExpressions;

namespace CafeHub.Domain.Tenants
{
    public sealed class Branch
    {
        private static readonly Regex NamePattern = new(
            "^[a-z0-9][a-z0-9- ]{1,38}[a-z0-9]$",
            RegexOptions.CultureInvariant);

        private Branch()
        {
        }

        public Guid Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public BranchLifecycleStatus LifecycleStatus { get; private set; }

        public string? Address { get; private set; }

        public string? ContactPhone { get; private set; }

        public string? OpeningHoursSummary { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static Branch Create(
            Guid id,
            string name,
            BranchLifecycleStatus lifecycleStatus,
            string? address,
            string? contactPhone,
            string? openingHoursSummary,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(id));
            }

            if (!Enum.IsDefined(lifecycleStatus))
            {
                throw new ArgumentException("The branch lifecycle status is not defined.", nameof(lifecycleStatus));
            }

            if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
            {
                throw new ArgumentException(
                    "The branch name must be between 1 and 256 characters.",
                    nameof(name));
            }

            if (!IsWellFormedName(name))
            {
                throw new ArgumentException(
                    "The branch name must start with a lowercase letter or digit and contain only lowercase letters, digits, spaces, and hyphens, never a leading or trailing hyphen.",
                    nameof(name));
            }

            if (address is not null && address.Length > 512)
            {
                throw new ArgumentException(
                    "The address must not exceed 512 characters.",
                    nameof(address));
            }

            if (contactPhone is not null && contactPhone.Length > 32)
            {
                throw new ArgumentException(
                    "The contact phone must not exceed 32 characters.",
                    nameof(contactPhone));
            }

            if (openingHoursSummary is not null && openingHoursSummary.Length > 512)
            {
                throw new ArgumentException(
                    "The opening hours summary must not exceed 512 characters.",
                    nameof(openingHoursSummary));
            }

            return new Branch
            {
                Id = id,
                Name = name,
                LifecycleStatus = lifecycleStatus,
                Address = address,
                ContactPhone = contactPhone,
                OpeningHoursSummary = openingHoursSummary,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        private static bool IsWellFormedName(string? name)
        {
            if (name is null || !NamePattern.IsMatch(name))
            {
                return false;
            }

            return true;
        }
    }
}
