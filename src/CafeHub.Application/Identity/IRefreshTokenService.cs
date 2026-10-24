using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Identity
{
    public interface IRefreshTokenService
    {
        Task<Result<IssuedRefreshToken>> IssueAsync(Guid adminId, CancellationToken cancellationToken);

        Task<Result<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken);

        Task<Result> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken);
    }
}
