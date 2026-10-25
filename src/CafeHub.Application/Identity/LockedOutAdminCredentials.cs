using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    /// <summary>
    /// The safe rejected-credentials error for an attempt denied because the administrator's account is
    /// locked out. The client-visible code, message, and type are identical to every other rejection, so
    /// lockout never changes the response body; the subtype only tells the audit trail why the attempt
    /// failed. The reason must never surface as a distinguishable client signal.
    /// </summary>
    public sealed record LockedOutAdminCredentials : Error
    {
        private LockedOutAdminCredentials(Guid administratorId)
            : base(AuthErrors.InvalidCredentialsCode, AuthErrors.InvalidCredentialsMessage, ErrorType.Unauthorized)
        {
            AdministratorId = administratorId;
        }

        public Guid AdministratorId { get; }

        public static LockedOutAdminCredentials Create(Guid administratorId)
        {
            if (administratorId == Guid.Empty)
            {
                throw new ArgumentException("The administrator id is required.", nameof(administratorId));
            }

            return new LockedOutAdminCredentials(administratorId);
        }
    }
}
