namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Single-quotes a PostgreSQL string literal for positions where the protocol does not allow
    /// parameters (role passwords). Control characters are rejected instead of escaped.
    /// </summary>
    public static class SqlLiteral
    {
        public static string Quote(string literal)
        {
            if (literal is null || literal.Any(char.IsControl))
            {
                throw new ArgumentException("The SQL literal is not valid.", nameof(literal));
            }

            return "'" + literal.Replace("'", "''") + "'";
        }
    }
}
