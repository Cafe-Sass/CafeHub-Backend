using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public interface IAdminAuthenticator
    {
        Task<Result<AuthenticatedAdmin>> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken);
    }
}
