using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    /// <summary>
    /// The safe rejected-credentials error for an attempt that identified a specific, active administrator,
    /// carrying that administrator's id for audit attribution only. The client-visible code, message, and
    /// type are identical to <see cref="AuthErrors.InvalidCredentials"/>, so every rejection maps to the same
    /// safe Unauthorized response and the carrier of the audit data stays internal.
    /// </summary>
    public sealed record RejectedAdminCredentials : Error
    {
        private RejectedAdminCredentials(Guid administratorId)
            : base(AuthErrors.InvalidCredentialsCode, AuthErrors.InvalidCredentialsMessage, ErrorType.Unauthorized)
        {
            AdministratorId = administratorId;
        }

        public Guid AdministratorId { get; }

        public static RejectedAdminCredentials Create(Guid administratorId)
        {
            if (administratorId == Guid.Empty)
            {
                throw new ArgumentException("The administrator id is required.", nameof(administratorId));
            }

            return new RejectedAdminCredentials(administratorId);
        }
    }
}
