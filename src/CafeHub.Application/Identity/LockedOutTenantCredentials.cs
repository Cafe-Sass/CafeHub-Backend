using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public sealed record LockedOutTenantCredentials : Error
    {
        private LockedOutTenantCredentials(Guid userId)
            : base(AuthErrors.InvalidCredentialsCode, AuthErrors.InvalidCredentialsMessage, ErrorType.Unauthorized)
        {
            UserId = userId;
        }

        public Guid UserId { get; }

        public static LockedOutTenantCredentials Create(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The tenant user id is required.", nameof(userId));
            }

            return new LockedOutTenantCredentials(userId);
        }
    }
}
