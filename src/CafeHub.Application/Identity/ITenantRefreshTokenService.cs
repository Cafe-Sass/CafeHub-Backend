using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public interface ITenantRefreshTokenService
    {
        public const int MaxRawTokenLength = 256;

        Task<Result<IssuedTenantRefreshToken>> IssueAsync(Guid userId, CancellationToken cancellationToken, string? expectedSessionVersion = null);

        Task<Result<RotatedTenantRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken);

        Task<Result> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken);
    }
}
