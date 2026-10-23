namespace CafeHub.WebApi.Correlation
{
    public static class CorrelationIdPolicy
    {
        public const int MaximumLength = 64;

        public static bool IsValid(string? correlationId)
        {
            if (string.IsNullOrEmpty(correlationId) || correlationId.Length > MaximumLength)
            {
                return false;
            }

            foreach (var character in correlationId)
            {
                if (!IsAllowedCharacter(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAllowedCharacter(char character)
        {
            return char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':';
        }
    }
}
