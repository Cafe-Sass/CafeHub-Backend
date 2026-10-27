using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.Commands;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Authentication;
using CafeHub.Infrastructure.Identity;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CafeHub.UnitTests
{
    public sealed class LoginTenantUserCommandTests
    {
        private static readonly Guid TenantId = Guid.NewGuid();

        [Fact]
        public async Task SuccessfulLogin_IssuesTenantBoundAccessAndRefreshTokens()
        {
            var user = new AuthenticatedTenantUser { Id = Guid.NewGuid(), Email = "owner@example.com", FullName = "Owner", UserKind = "Owner", Roles = new[] { "Owner" } };
            var issuer = new StubTenantTokenIssuer();
            var refresh = new StubTenantRefreshTokenService();
            var handler = new LoginTenantUserCommandHandler(new StubTenantAuthenticator(Result.Success(user)), issuer, refresh, new StubTenantContextAccessor(TenantId));

            var result = await handler.Handle(new LoginTenantUserCommand("owner@example.com", "secret-password"), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(TenantId, Assert.Single(issuer.Requests).TenantId);
            Assert.Equal(user.Id, Assert.Single(refresh.UserIds));
            Assert.Equal("access", result.Value!.AccessToken);
            Assert.Equal("refresh", result.Value.RefreshToken);
            Assert.Equal(4, typeof(TenantTokenResponse).GetProperties().Length);
        }

        [Fact]
        public async Task FailedCredentials_ReturnTheAuthenticatorErrorWithoutIssuingTokens()
        {
            var error = AuthErrors.InvalidCredentials();
            var issuer = new StubTenantTokenIssuer();
            var refresh = new StubTenantRefreshTokenService();
            var handler = new LoginTenantUserCommandHandler(new StubTenantAuthenticator(Result.Failure<AuthenticatedTenantUser>(error)), issuer, refresh, new StubTenantContextAccessor(TenantId));

            var result = await handler.Handle(new LoginTenantUserCommand("owner@example.com", "wrong"), CancellationToken.None);

            Assert.Equal(error, result.Error);
            Assert.Empty(issuer.Requests);
            Assert.Empty(refresh.UserIds);
        }

        [Fact]
        public void TenantTokenOptions_RejectPlatformAudienceAndInvalidLifetimes()
        {
            Assert.Empty(new TenantJwtOptions { Audience = "tenant-api" }.Validate("platform-api"));
            Assert.NotEmpty(new TenantJwtOptions { Audience = "platform-api" }.Validate("platform-api"));
            Assert.NotEmpty(new TenantJwtOptions { Audience = "tenant-api", AccessTokenLifetimeMinutes = 0 }.Validate("platform-api"));
            Assert.NotEmpty(new TenantJwtOptions { Audience = "tenant-api", AccessTokenLifetimeMinutes = 61 }.Validate("platform-api"));
        }

        [Fact]
        public void TenantAccessToken_ContainsTenantClaimsAndExpiresAtConfiguredTime()
        {
            var now = DateTimeOffset.Parse("2026-10-27T10:00:00Z");
            var issuer = new TenantJwtAccessTokenIssuer(new JwtOptions { Issuer = "issuer", Audience = "platform", SigningKey = new string('k', 32) }, new TenantJwtOptions { Audience = "tenant", AccessTokenLifetimeMinutes = 15 }, new FixedTimeProvider(now));
            var userId = Guid.NewGuid();
            var token = issuer.Issue(new TenantAccessTokenRequest { UserId = userId, Email = "owner@example.com", Roles = new[] { "Owner", "Manager" }, TenantId = TenantId });
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token.Value);

            Assert.Equal(now.AddMinutes(15), token.ExpiresAt);
            Assert.Equal("tenant", jwt.Audiences.Single());
            Assert.Equal(TenantId.ToString(), jwt.GetClaim("tid")?.Value);
            Assert.Equal("tenant", jwt.GetClaim("token_kind")?.Value);
            Assert.Single(jwt.Claims, claim => claim.Type == "iat");
            Assert.Equal(userId.ToString(), jwt.GetClaim("sub")?.Value);
            Assert.Equal("owner@example.com", jwt.GetClaim("email")?.Value);
            Assert.NotEqual("platform", jwt.Audiences.Single());
            Assert.Equal(new[] { "Owner", "Manager" }, jwt.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value));
            Assert.DoesNotContain(jwt.Claims, claim => claim.Type.Contains("password", StringComparison.OrdinalIgnoreCase) || claim.Type.Contains("stamp", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void LoginEndpoint_IsAnonymousAndRateLimited()
        {
            var method = typeof(TenantAuthController).GetMethod(nameof(TenantAuthController.Login))!;
            Assert.NotNull(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).SingleOrDefault());
            Assert.NotNull(typeof(TenantAuthController).GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).SingleOrDefault());
            Assert.Equal("t/{slug}/api/v1/auth", typeof(TenantAuthController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), true).Cast<Microsoft.AspNetCore.Mvc.RouteAttribute>().Single().Template);
        }

        [Fact]
        public async Task TenantRefreshIssuance_PersistsOnlyTheTokenHash()
        {
            var tokenStore = new CapturingTenantRefreshStore();
            var service = new TenantRefreshTokenService(tokenStore, new RecordingTenantUnitOfWork(), new FixedTokenGenerator(), new FixedTokenHasher(), new RefreshTokenOptions(), new FixedTimeProvider(DateTimeOffset.Parse("2026-10-27T10:00:00Z")), NullLogger<TenantRefreshTokenService>.Instance);

            var result = await service.IssueAsync(Guid.NewGuid(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var stored = Assert.IsType<TenantRefreshToken>(tokenStore.Token);
            Assert.NotEqual(result.Value!.RawToken, stored.TokenHash);
            Assert.Equal(new string('a', 64), stored.TokenHash);
            Assert.Equal(stored.FamilyId, result.Value.FamilyId);
        }

        [Fact]
        public void LoginValidator_RejectsMissingAndOversizedCredentialsWithoutEchoingPassword()
        {
            var validator = new LoginTenantUserCommandValidator();
            var password = new string('p', 129);

            var result = validator.Validate(new LoginTenantUserCommand("bad-email", password));

            Assert.False(result.IsValid);
            Assert.DoesNotContain(result.Errors, error => error.ErrorMessage.Contains(password, StringComparison.Ordinal));
        }

        [Fact]
        public async Task MissingTenantContext_FailsClosedBeforeTokenIssuance()
        {
            var user = new AuthenticatedTenantUser { Id = Guid.NewGuid(), Email = "owner@example.com", FullName = "Owner", UserKind = "Owner", Roles = Array.Empty<string>() };
            var issuer = new StubTenantTokenIssuer();
            var handler = new LoginTenantUserCommandHandler(new StubTenantAuthenticator(Result.Success(user)), issuer, new StubTenantRefreshTokenService(), new MissingTenantContextAccessor());

            await Assert.ThrowsAsync<TenantContextMissingException>(() => handler.Handle(new LoginTenantUserCommand("owner@example.com", "password"), CancellationToken.None));
            Assert.Empty(issuer.Requests);
        }

        [Fact]
        public void TenantStoresExposeIdentityRoleAndClaimContractsWithoutChangingEntityInheritance()
        {
            Assert.Contains(typeof(IUserRoleStore<TenantUser>), typeof(TenantUserStore).GetInterfaces());
            Assert.Contains(typeof(IUserClaimStore<TenantUser>), typeof(TenantUserStore).GetInterfaces());
            Assert.Contains(typeof(IUserLockoutStore<TenantUser>), typeof(TenantUserStore).GetInterfaces());
            Assert.Contains(typeof(IUserSecurityStampStore<TenantUser>), typeof(TenantUserStore).GetInterfaces());
            Assert.Contains(typeof(IRoleClaimStore<TenantRole>), typeof(TenantRoleStore).GetInterfaces());
            Assert.NotEqual(typeof(Microsoft.AspNetCore.Identity.IdentityUser<Guid>), typeof(TenantUser).BaseType);
        }

        private sealed class StubTenantAuthenticator(Result<AuthenticatedTenantUser> result) : ITenantUserAuthenticator
        {
            public Task<Result<AuthenticatedTenantUser>> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken) => Task.FromResult(result);
            public Task<AuthenticatedTenantUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<AuthenticatedTenantUser?>(result.IsSuccess ? result.Value : null);
        }

        private sealed class StubTenantTokenIssuer : ITenantAccessTokenIssuer
        {
            public List<TenantAccessTokenRequest> Requests { get; } = new();
            public AccessToken Issue(TenantAccessTokenRequest request) { Requests.Add(request); return new AccessToken { Value = "access", ExpiresAt = DateTimeOffset.UtcNow }; }
        }

        private sealed class StubTenantRefreshTokenService : ITenantRefreshTokenService
        {
            public List<Guid> UserIds { get; } = new();
            public Task<Result<IssuedTenantRefreshToken>> IssueAsync(Guid userId, CancellationToken cancellationToken) { UserIds.Add(userId); return Task.FromResult(Result.Success(new IssuedTenantRefreshToken { RawToken = "refresh", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7), FamilyId = Guid.NewGuid() })); }
            public Task<Result<RotatedTenantRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken) => Task.FromResult(Result.Failure<RotatedTenantRefreshToken>(AuthErrors.InvalidRefreshToken()));
            public Task<Result<Guid>> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken) => Task.FromResult(Result.Failure<Guid>(AuthErrors.InvalidRefreshToken()));
        }

        private sealed class StubTenantContextAccessor(Guid tenantId) : ITenantContextAccessor
        {
            public TenantContext GetRequired() => TenantContext.Create(tenantId, "tenant-secret-ref", true);
        }

        private sealed class MissingTenantContextAccessor : ITenantContextAccessor
        {
            public TenantContext GetRequired() => throw new TenantContextMissingException("Missing tenant context.");
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class FixedTokenGenerator : IRefreshTokenGenerator
        {
            public string Generate() => "raw-refresh-token-secret";
        }

        private sealed class FixedTokenHasher : ITokenHasher
        {
            public string Hash(string rawToken) => new string('a', 64);
        }

        private sealed class CapturingTenantRefreshStore : ITenantRefreshTokenStore
        {
            public TenantRefreshToken? Token { get; private set; }
            public Task<TenantRefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(Token is not null && Token.TokenHash == tokenHash ? Token : null);
            public Task<IReadOnlyList<TenantRefreshToken>> FindFamilyAsync(Guid familyId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<TenantRefreshToken>>(Token is not null && Token.FamilyId == familyId ? new[] { Token } : Array.Empty<TenantRefreshToken>());
            public void Add(TenantRefreshToken token) => Token = token;
            public void ClearTracking() { }
        }

        private sealed class RecordingTenantUnitOfWork : ITenantUnitOfWork
        {
            public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
