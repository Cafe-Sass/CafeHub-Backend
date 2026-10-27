using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public sealed record RejectedTenantCredentials : Error
    {
        private RejectedTenantCredentials(Guid userId)
            : base(AuthErrors.InvalidCredentialsCode, AuthErrors.InvalidCredentialsMessage, ErrorType.Unauthorized)
        {
            UserId = userId;
        }

        public Guid UserId { get; }

        public static RejectedTenantCredentials Create(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The tenant user id is required.", nameof(userId));
            }

            return new RejectedTenantCredentials(userId);
        }
    }
}
