using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public interface IRefreshTokenService
    {
        public const int MaxRawTokenLength = 512;

        Task<Result<IssuedRefreshToken>> IssueAsync(Guid adminId, CancellationToken cancellationToken);

        Task<Result<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken);

        Task<Result<Guid>> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken);
    }
}
