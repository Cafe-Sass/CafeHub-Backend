using System.Text.RegularExpressions;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Validates and double-quotes a PostgreSQL identifier. Every identifier in generated SQL must pass
    /// through this helper; an identifier that does not match the allowed pattern is never quoted or used.
    /// </summary>
    public static partial class SqlIdentifier
    {
        public const string Pattern = "^[a-z][a-z0-9_]{0,62}$";

        [GeneratedRegex(Pattern)]
        private static partial Regex IdentifierRegex();

        public static bool IsValid(string identifier)
        {
            return !string.IsNullOrEmpty(identifier) && IdentifierRegex().IsMatch(identifier);
        }

        public static string Quote(string identifier)
        {
            if (!IsValid(identifier))
            {
                throw new ArgumentException("The SQL identifier is not valid.", nameof(identifier));
            }

            return "\"" + identifier + "\"";
        }
    }
}
