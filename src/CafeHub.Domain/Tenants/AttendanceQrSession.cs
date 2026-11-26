namespace CafeHub.Domain.Tenants
{
    public sealed class AttendanceQrSession
    {
        public const int TokenBytes = 32;
        public const int TokenLength = 43;
        public const int LifetimeMinutes = 10;

        private AttendanceQrSession()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public string Token { get; private set; } = string.Empty;

        public DateTimeOffset ValidFrom { get; private set; }

        public DateTimeOffset ValidTo { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public Guid CreatedBy { get; private set; }

        public static AttendanceQrSession Create(
            Guid id,
            Guid branchId,
            string token,
            DateTimeOffset now,
            Guid createdBy)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The attendance QR session id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (createdBy == Guid.Empty)
            {
                throw new ArgumentException("The creating user id is required.", nameof(createdBy));
            }

            return new AttendanceQrSession
            {
                Id = id,
                BranchId = branchId,
                Token = ValidateToken(token),
                ValidFrom = now,
                ValidTo = ExpiryFor(now),
                CreatedAt = now,
                CreatedBy = createdBy,
            };
        }

        public bool IsValid(DateTimeOffset now) => now >= ValidFrom && now < ValidTo;

        public static AttendanceQrSession? Current(
            IEnumerable<AttendanceQrSession> sessions, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(sessions);

            return sessions
                .Where(session => session.IsValid(now))
                .OrderByDescending(session => session.ValidTo)
                .ThenByDescending(session => session.Id)
                .FirstOrDefault();
        }

        public static DateTimeOffset ExpiryFor(DateTimeOffset now) => now.AddMinutes(LifetimeMinutes);

        public static bool IsValidToken(string? token) =>
            token is { Length: TokenLength }
            && token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

        private static string ValidateToken(string token)
        {
            if (!IsValidToken(token))
            {
                throw new ArgumentException(
                    $"The attendance QR token must be {TokenLength} URL-safe base64url characters.",
                    nameof(token));
            }

            return token;
        }
    }
}
