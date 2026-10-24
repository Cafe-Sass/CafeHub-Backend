using System.Security.Claims;
using CafeHub.WebApi;
using CafeHub.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests
{
    public sealed class PlatformAuthorizationPoliciesTests
    {
        private static IAuthorizationService CreateAuthorizationService()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPlatformAuthorization();

            return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
        }

        private static ClaimsPrincipal CreatePrincipal(params string[] roles)
        {
            var claims = roles.Select(role => new Claim("role", role));
            var identity = new ClaimsIdentity(claims, authenticationType: "Test", nameType: "sub", roleType: "role");

            return new ClaimsPrincipal(identity);
        }

        [Fact]
        public void PolicyNames_AreTheDocumentedConstants()
        {
            Assert.Equal("PlatformAdmin", PlatformAuthorizationPolicies.PlatformAdmin);
            Assert.Equal("SuperAdmin", PlatformAuthorizationPolicies.SuperAdmin);
        }

        [Fact]
        public async Task SuperAdminPrincipal_SatisfiesBothPolicies()
        {
            var authorizationService = CreateAuthorizationService();
            var principal = CreatePrincipal("SuperAdmin");

            var platformAdminResult = await authorizationService.AuthorizeAsync(principal, PlatformAuthorizationPolicies.PlatformAdmin);
            var superAdminResult = await authorizationService.AuthorizeAsync(principal, PlatformAuthorizationPolicies.SuperAdmin);

            Assert.True(platformAdminResult.Succeeded);
            Assert.True(superAdminResult.Succeeded);
        }

        [Fact]
        public async Task WrongRolePrincipal_SatisfiesNeitherPolicy()
        {
            var authorizationService = CreateAuthorizationService();
            var principal = CreatePrincipal("Operations");

            var platformAdminResult = await authorizationService.AuthorizeAsync(principal, PlatformAuthorizationPolicies.PlatformAdmin);
            var superAdminResult = await authorizationService.AuthorizeAsync(principal, PlatformAuthorizationPolicies.SuperAdmin);

            Assert.False(platformAdminResult.Succeeded);
            Assert.False(superAdminResult.Succeeded);
        }

        [Fact]
        public async Task AnonymousPrincipal_SatisfiesNeitherPolicy()
        {
            var authorizationService = CreateAuthorizationService();
            var principal = new ClaimsPrincipal(new ClaimsIdentity());

            var platformAdminResult = await authorizationService.AuthorizeAsync(principal, PlatformAuthorizationPolicies.PlatformAdmin);
            var superAdminResult = await authorizationService.AuthorizeAsync(principal, PlatformAuthorizationPolicies.SuperAdmin);

            Assert.False(platformAdminResult.Succeeded);
            Assert.False(superAdminResult.Succeeded);
        }

        [Fact]
        public async Task AuthenticatedPrincipalWithoutRoles_SatisfiesNeitherPolicy()
        {
            var authorizationService = CreateAuthorizationService();
            var principal = CreatePrincipal();

            var platformAdminResult = await authorizationService.AuthorizeAsync(principal, PlatformAuthorizationPolicies.PlatformAdmin);
            var superAdminResult = await authorizationService.AuthorizeAsync(principal, PlatformAuthorizationPolicies.SuperAdmin);

            Assert.False(platformAdminResult.Succeeded);
            Assert.False(superAdminResult.Succeeded);
        }
    }
}
