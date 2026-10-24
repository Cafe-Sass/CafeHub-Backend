using System.Security.Claims;
using CafeHub.WebApi.Authorization;
using Microsoft.AspNetCore.Http;

namespace CafeHub.UnitTests
{
    public sealed class HttpContextCurrentUserTests
    {
        private static HttpContextCurrentUser CreateCurrentUser(ClaimsPrincipal? principal)
        {
            var httpContext = principal is null ? null : new DefaultHttpContext { User = principal };

            return new HttpContextCurrentUser(new HttpContextAccessor { HttpContext = httpContext });
        }

        private static ClaimsPrincipal CreateAuthenticatedPrincipal(string? subClaimValue, params string[] roles)
        {
            var claims = new List<Claim>();
            if (subClaimValue is not null)
            {
                claims.Add(new Claim("sub", subClaimValue));
            }

            claims.AddRange(roles.Select(role => new Claim("role", role)));

            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test", nameType: "sub", roleType: "role"));
        }

        [Fact]
        public void AuthenticatedPrincipal_ReturnsIsAuthenticated_AdminId_AndRoles()
        {
            var adminId = Guid.NewGuid();
            var currentUser = CreateCurrentUser(CreateAuthenticatedPrincipal(adminId.ToString(), "SuperAdmin", "Operations"));

            Assert.True(currentUser.IsAuthenticated);
            Assert.Equal(adminId, currentUser.AdminId);
            Assert.Equal(new[] { "SuperAdmin", "Operations" }, currentUser.Roles);
        }

        [Fact]
        public void MissingHttpContext_ReturnsNoIdentity_NoAdminId_AndNoRoles()
        {
            var currentUser = CreateCurrentUser(null);

            Assert.False(currentUser.IsAuthenticated);
            Assert.Null(currentUser.AdminId);
            Assert.Empty(currentUser.Roles);
        }

        [Fact]
        public void UnauthenticatedPrincipalWithClaims_ReturnsNoIdentity_NoAdminId_AndNoRoles()
        {
            var unauthenticatedIdentity = new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()),
                new Claim("role", "SuperAdmin"),
            });

            var currentUser = CreateCurrentUser(new ClaimsPrincipal(unauthenticatedIdentity));

            Assert.False(currentUser.IsAuthenticated);
            Assert.Null(currentUser.AdminId);
            Assert.Empty(currentUser.Roles);
        }

        [Fact]
        public void NonGuidSub_ReturnsIsAuthenticated_AndRoles_ButNoAdminId()
        {
            var currentUser = CreateCurrentUser(CreateAuthenticatedPrincipal("not-a-guid", "SuperAdmin"));

            Assert.True(currentUser.IsAuthenticated);
            Assert.Null(currentUser.AdminId);
            Assert.Equal(new[] { "SuperAdmin" }, currentUser.Roles);
        }

        [Fact]
        public void AuthenticatedPrincipalWithoutSub_ReturnsNoAdminId()
        {
            var currentUser = CreateCurrentUser(CreateAuthenticatedPrincipal(null, "SuperAdmin"));

            Assert.True(currentUser.IsAuthenticated);
            Assert.Null(currentUser.AdminId);
            Assert.Equal(new[] { "SuperAdmin" }, currentUser.Roles);
        }
    }
}
