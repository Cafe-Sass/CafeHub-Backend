using CafeHub.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Identity
{
    /// <summary>
    /// Lookup normalization for platform admin identity. Emails go through the repository's email
    /// decision (<see cref="PlatformAdminEmail"/>: trimmed and lowercased), so find-by-email matches
    /// how identity rows are stored; user names keep the framework's upper-invariant normalization.
    /// </summary>
    public sealed class PlatformAdminLookupNormalizer : ILookupNormalizer
    {
        public string? NormalizeName(string? name)
        {
            return name?.ToUpperInvariant();
        }

        public string? NormalizeEmail(string? email)
        {
            return email is null ? null : PlatformAdminEmail.Normalize(email);
        }
    }
}
