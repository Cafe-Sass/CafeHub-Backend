using System.Text.RegularExpressions;

namespace CafeHub.Domain.Tenants
{
    public sealed class TenantUser
    {
        private static readonly Regex UserNamePattern = new(
            "^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> ReservedUserNames = new(
            new[]
            {
                "admin", "api", "t", "www", "app", "static", "assets",
                "health", "login", "signup", "support", "platform",
            },
            StringComparer.Ordinal);

        private TenantUser()
        {
        }

        public Guid Id { get; private set; }

        public TenantUserKind UserKind { get; private set; }

        public string UserName { get; private set; } = string.Empty;

        public string NormalizedUserName { get; private set; } = string.Empty;

        public string FullName { get; private set; } = string.Empty;

        public string? Email { get; private set; }

        public string? NormalizedEmail { get; private set; }

        public bool EmailConfirmed { get; private set; }

        public string? PhoneNumber { get; private set; }

        public bool PhoneNumberConfirmed { get; private set; }

        public string PasswordHash { get; private set; } = string.Empty;

        public bool TwoFactorEnabled { get; private set; }

        public bool LockoutEnabled { get; private set; }

        public DateTimeOffset? LockoutEndAt { get; private set; }

        public int AccessFailedCount { get; private set; }

        public string SecurityStamp { get; private set; } = string.Empty;

        public string ConcurrencyStamp { get; private set; } = string.Empty;

        public DateTimeOffset? DeletedAt { get; private set; }

        public Guid? DeletedBy { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static TenantUser Create(
            Guid id,
            TenantUserKind userKind,
            string userName,
            string normalizedUserName,
            string fullName,
            string? email,
            string? normalizedEmail,
            string? phoneNumber,
            string passwordHash,
            string securityStamp,
            string concurrencyStamp,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant user id is required.", nameof(id));
            }

            if (!Enum.IsDefined(userKind))
            {
                throw new ArgumentException("The tenant user kind is not defined.", nameof(userKind));
            }

            if (!IsWellFormedUserName(userName))
            {
                throw new ArgumentException(
                    "The user name must be 3 to 40 characters of lowercase letters and digits with optional internal hyphens, and must not be a reserved word.",
                    nameof(userName));
            }

            if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 256)
            {
                throw new ArgumentException(
                    "The full name must be between 1 and 256 characters.",
                    nameof(fullName));
            }

            if (email is not null && (string.IsNullOrWhiteSpace(email) || email.Length > 320))
            {
                throw new ArgumentException(
                    "The email, when present, must be between 1 and 320 characters.",
                    nameof(email));
            }

            if (normalizedEmail is not null && normalizedEmail.Length > 320)
            {
                throw new ArgumentException(
                    "The normalized email, when present, must not exceed 320 characters.",
                    nameof(normalizedEmail));
            }

            if (phoneNumber is not null && (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length > 64))
            {
                throw new ArgumentException(
                    "The phone number, when present, must be between 1 and 64 characters.",
                    nameof(phoneNumber));
            }

            if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > 512)
            {
                throw new ArgumentException(
                    "The password hash must be between 1 and 512 characters.",
                    nameof(passwordHash));
            }

            if (string.IsNullOrWhiteSpace(securityStamp) || securityStamp.Length > 64)
            {
                throw new ArgumentException(
                    "The security stamp must be between 1 and 64 characters.",
                    nameof(securityStamp));
            }

            if (string.IsNullOrWhiteSpace(concurrencyStamp) || concurrencyStamp.Length > 64)
            {
                throw new ArgumentException(
                    "The concurrency stamp must be between 1 and 64 characters.",
                    nameof(concurrencyStamp));
            }

            return new TenantUser
            {
                Id = id,
                UserKind = userKind,
                UserName = userName,
                NormalizedUserName = normalizedUserName,
                FullName = fullName,
                Email = email,
                NormalizedEmail = normalizedEmail,
                EmailConfirmed = false,
                PhoneNumber = phoneNumber,
                PhoneNumberConfirmed = false,
                PasswordHash = passwordHash,
                TwoFactorEnabled = false,
                LockoutEnabled = true,
                LockoutEndAt = null,
                AccessFailedCount = 0,
                SecurityStamp = securityStamp,
                ConcurrencyStamp = concurrencyStamp,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        private static bool IsWellFormedUserName(string? userName)
        {
            if (userName is null || !UserNamePattern.IsMatch(userName))
            {
                return false;
            }

            return !ReservedUserNames.Contains(userName);
        }
    }
}
