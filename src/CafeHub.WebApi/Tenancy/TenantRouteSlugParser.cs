using CafeHub.Domain.Onboarding;
using Microsoft.AspNetCore.Http;

namespace CafeHub.WebApi.Tenancy
{
    /// <summary>
    /// Parses the slug out of a /t/{slug}/... request path without any I/O. A path yields a slug only when
    /// its first segment is "t" case-insensitively and the second segment is non-empty; segments longer than
    /// the slug policy maximum or containing percent escapes, dots, slashes, or whitespace yield no slug.
    /// The parser never validates the slug against the full policy; the resolver does that.
    /// </summary>
    public static class TenantRouteSlugParser
    {
        public static bool TryGetSlug(PathString path, out string slug)
        {
            slug = string.Empty;

            if (!path.HasValue)
            {
                return false;
            }

            var segments = path.Value!.Split('/');
            if (segments.Length < 3
                || !segments[1].Equals("t", StringComparison.OrdinalIgnoreCase)
                || segments[2].Length == 0)
            {
                return false;
            }

            var candidate = segments[2];
            if (candidate.Length > TenantSlug.MaxLength
                || candidate.Contains('%')
                || candidate.Contains('.')
                || ContainsWhitespace(candidate))
            {
                return false;
            }

            slug = candidate;
            return true;
        }

        private static bool ContainsWhitespace(string value)
        {
            foreach (var character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
