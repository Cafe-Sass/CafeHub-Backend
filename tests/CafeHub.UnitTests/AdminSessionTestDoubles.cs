using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;

namespace CafeHub.UnitTests
{
    internal sealed class SessionRefreshTokenServiceStub : IRefreshTokenService
    {
        public Result<RotatedRefreshToken> NextRotation { get; set; } =
            Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken());

        public Result<Guid> NextRevocation { get; set; } =
            Result.Failure<Guid>(AuthErrors.InvalidRefreshToken());

        public List<string> RotatedRawTokens { get; } = new();

        public List<RefreshTokenRevocationCall> Revocations { get; } = new();

        public Task<Result<IssuedRefreshToken>> IssueAsync(Guid adminId, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<Result<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken)
        {
            RotatedRawTokens.Add(rawToken);

            return Task.FromResult(NextRotation);
        }

        public Task<Result<Guid>> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken)
        {
            Revocations.Add(new RefreshTokenRevocationCall(rawToken, reason));

            return Task.FromResult(NextRevocation);
        }
    }

    internal sealed record RefreshTokenRevocationCall(string RawToken, string Reason);

    internal sealed class StubAdminAccountReader : IAdminAccountReader
    {
        public AuthenticatedAdmin? NextAdmin { get; set; }

        public List<Guid> RequestedAdminIds { get; } = new();

        public Task<AuthenticatedAdmin?> FindActiveByIdAsync(Guid adminId, CancellationToken cancellationToken)
        {
            RequestedAdminIds.Add(adminId);

            return Task.FromResult(NextAdmin);
        }
    }
}
