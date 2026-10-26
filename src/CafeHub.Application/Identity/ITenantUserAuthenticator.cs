using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public interface ITenantUserAuthenticator
    {
        Task<Result<AuthenticatedTenantUser>> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken);
    }
}
