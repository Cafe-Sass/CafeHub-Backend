using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public interface ITenantRefreshTokenService
    {
        Task<Result<IssuedTenantRefreshToken>> IssueAsync(Guid userId, CancellationToken cancellationToken);
    }
}
