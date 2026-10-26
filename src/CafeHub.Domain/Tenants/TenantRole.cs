using System.Text.RegularExpressions;

namespace CafeHub.Domain.Tenants
{
    public sealed class TenantRole
    {
        private static readonly Regex NamePattern = new(
            "^[a-z][a-z0-9_-]{1,63}$",
            RegexOptions.CultureInvariant);

        private TenantRole()
        {
        }

        public Guid Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public string NormalizedName { get; private set; } = string.Empty;

        public string ConcurrencyStamp { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static TenantRole Create(
            Guid id,
            string name,
            string normalizedName,
            string concurrencyStamp,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant role id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
            {
                throw new ArgumentException(
                    "The role name must be between 1 and 256 characters.",
                    nameof(name));
            }

            if (!IsWellFormedName(name))
            {
                throw new ArgumentException(
                    "The role name must start with a lowercase letter and contain only lowercase letters, digits, underscores, and hyphens.",
                    nameof(name));
            }

            if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 256)
            {
                throw new ArgumentException(
                    "The normalized role name must be between 1 and 256 characters.",
                    nameof(normalizedName));
            }

            if (string.IsNullOrWhiteSpace(concurrencyStamp) || concurrencyStamp.Length > 64)
            {
                throw new ArgumentException(
                    "The concurrency stamp must be between 1 and 64 characters.",
                    nameof(concurrencyStamp));
            }

            return new TenantRole
            {
                Id = id,
                Name = name,
                NormalizedName = normalizedName,
                ConcurrencyStamp = concurrencyStamp,
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
