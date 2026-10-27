using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Identity.Commands;
using CafeHub.Application.Identity.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Identity;
using CafeHub.Infrastructure.Authentication;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeHub.UnitTests
{
    public sealed class TenantRefreshAndLogoutCommandTests
    {
        [Fact]
        public async Task Refresh_RotatesAndIssuesTenantAccessTokenUsingLatestRoles()
        {
            var user = User(Guid.NewGuid(), "Manager");
            var refresh = new StubRefreshService(Result.Success(new RotatedTenantRefreshToken { RawToken = "rotated-secret", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7), UserId = user.Id }));
            var issuer = new StubIssuer();
            var handler = new RefreshTenantTokenCommandHandler(refresh, new StubAuthenticator(user), issuer, new TenantContextAccessor());

            var result = await handler.Handle(new RefreshTenantTokenCommand("presented-secret"), default);

            Assert.True(result.IsSuccess);
            Assert.Equal("rotated-secret", result.Value.RefreshToken);
            Assert.Equal("access", result.Value.AccessToken);
            Assert.Equal(new[] { "Manager" }, Assert.Single(issuer.Requests).Roles);
            Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), issuer.Requests[0].TenantId);
            Assert.Equal("presented-secret", refresh.RotatedRawToken);
        }

        [Fact]
        public async Task Refresh_RevokesRotatedFamilyWhenOwnerIsMissingOrDeleted()
        {
            var id = Guid.NewGuid();
            var refresh = new StubRefreshService(Result.Success(new RotatedTenantRefreshToken { RawToken = "rotated-secret", ExpiresAt = DateTimeOffset.UtcNow.AddDays(7), UserId = id }));
            var issuer = new StubIssuer();
            var handler = new RefreshTenantTokenCommandHandler(refresh, new StubAuthenticator(null), issuer, new TenantContextAccessor());

            var result = await handler.Handle(new RefreshTenantTokenCommand("presented-secret"), default);

            Assert.False(result.IsSuccess);
            Assert.Equal(AuthErrors.InvalidRefreshToken(), result.Error);
            Assert.Equal(("rotated-secret", RefreshTokenRevocationReasons.AdminRevoked), refresh.Revocation);
            Assert.Empty(issuer.Requests);
        }

        [Fact]
        public async Task Refresh_MapsAnyRotationFailureToGenericInvalidRefreshError()
        {
            var handler = new RefreshTenantTokenCommandHandler(new StubRefreshService(Result.Failure<RotatedTenantRefreshToken>(AuthErrors.InvalidRefreshToken())), new StubAuthenticator(null), new StubIssuer(), new TenantContextAccessor());

            var result = await handler.Handle(new RefreshTenantTokenCommand("secret"), default);

            Assert.Equal(AuthErrors.InvalidRefreshToken(), result.Error);
        }

        [Fact]
        public async Task Logout_IsIdempotentForUnknownOrAlreadyRevokedToken()
        {
            var service = new StubRefreshService(Result.Failure<RotatedTenantRefreshToken>(AuthErrors.InvalidRefreshToken()));
            var handler = new LogoutTenantUserCommandHandler(service);

            var result = await handler.Handle(new LogoutTenantUserCommand("unknown"), default);

            Assert.True(result.IsSuccess);
            Assert.Equal(("unknown", RefreshTokenRevocationReasons.Logout), service.Revocation);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void RefreshAndLogoutValidatorsRejectMissingToken(string? token)
        {
            Assert.False(new RefreshTenantTokenCommandValidator().Validate(new RefreshTenantTokenCommand(token!)).IsValid);
            Assert.False(new LogoutTenantUserCommandValidator().Validate(new LogoutTenantUserCommand(token!)).IsValid);
        }

        [Fact]
        public void RefreshAndLogoutValidatorsRejectOversizedTokenWithoutEchoingIt()
        {
            var raw = new string('s', 257);
            var refresh = new RefreshTenantTokenCommandValidator().Validate(new RefreshTenantTokenCommand(raw));
            var logout = new LogoutTenantUserCommandValidator().Validate(new LogoutTenantUserCommand(raw));
            Assert.False(refresh.IsValid);
            Assert.False(logout.IsValid);
            Assert.DoesNotContain(refresh.Errors, e => e.ErrorMessage.Contains(raw, StringComparison.Ordinal));
            Assert.DoesNotContain(logout.Errors, e => e.ErrorMessage.Contains(raw, StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(nameof(TenantAuthController.Refresh))]
        [InlineData(nameof(TenantAuthController.Logout))]
        public void RefreshAndLogoutEndpointsAreAnonymousAndRateLimited(string actionName)
        {
            var method = typeof(TenantAuthController).GetMethod(actionName)!;
            Assert.NotNull(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true).SingleOrDefault());
            Assert.NotNull(typeof(TenantAuthController).GetCustomAttributes(typeof(EnableRateLimitingAttribute), true).SingleOrDefault());
            Assert.Single(method.GetCustomAttributes(typeof(HttpPostAttribute), true));
        }

        private static AuthenticatedTenantUser User(Guid id, params string[] roles) => new() { Id = id, Email = "owner@example.com", FullName = "Owner", UserKind = "Owner", Roles = roles };

        private sealed class StubRefreshService(Result<RotatedTenantRefreshToken> rotationResult) : ITenantRefreshTokenService
        {
            public string? RotatedRawToken { get; private set; }
            public (string Token, string Reason)? Revocation { get; private set; }
            public Task<Result<IssuedTenantRefreshToken>> IssueAsync(Guid userId, CancellationToken cancellationToken) => throw new NotImplementedException();
            public Task<Result<RotatedTenantRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken) { RotatedRawToken = rawToken; return Task.FromResult(rotationResult); }
            public Task<Result<Guid>> RevokeFamilyAsync(string rawToken, string reason, CancellationToken cancellationToken) { Revocation = (rawToken, reason); return Task.FromResult(Result.Failure<Guid>(AuthErrors.InvalidRefreshToken())); }
        }

        private sealed class StubAuthenticator(AuthenticatedTenantUser? user) : ITenantUserAuthenticator
        {
            public Task<Result<AuthenticatedTenantUser>> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken) => throw new NotImplementedException();
            public Task<AuthenticatedTenantUser?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(user);
        }

        private sealed class StubIssuer : ITenantAccessTokenIssuer
        {
            public List<TenantAccessTokenRequest> Requests { get; } = [];
            public AccessToken Issue(TenantAccessTokenRequest request) { Requests.Add(request); return new AccessToken { Value = "access", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) }; }
        }

        private sealed class TenantContextAccessor : ITenantContextAccessor
        {
            public TenantContext GetRequired() => TenantContext.Create(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "reference", true);
        }
    }
}
