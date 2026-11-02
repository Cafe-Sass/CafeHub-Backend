using System.Security.Claims;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.WebApi;
using CafeHub.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Isolation
{
    /// <summary>
    /// Proves tenant isolation at the authorization boundary.
    /// Note: The data-access boundary (a real database per tenant) is NOT proven by automated unit tests;
    /// it is covered by documented manual verification.
    /// </summary>
    public sealed class TenantAuthorizationBoundaryIsolationTests
    {
        private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        private static readonly Guid UserAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid UserBId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static ServiceProvider CreateTenantServices(Guid contextTenantId)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ITenantContextAccessor>(new FakeTenantContextAccessor(contextTenantId));
            services.AddTenantAuthorization();
            return services.BuildServiceProvider();
        }

        private static ServiceProvider CreatePlatformServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPlatformAuthorization();
            return services.BuildServiceProvider();
        }

        private static ClaimsPrincipal CreateTenantPrincipal(Guid tenantId, Guid userId, params string[] roles)
        {
            var claims = new List<Claim>
            {
                new("tid", tenantId.ToString()),
                new("sub", userId.ToString()),
            };
            claims.AddRange(roles.Select(role => new Claim("role", role)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TenantAuthTest", "sub", "role"));
        }

        private static ClaimsPrincipal CreatePlatformPrincipal(params string[] roles)
        {
            var claims = roles.Select(role => new Claim("role", role));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "PlatformAuthTest", "sub", "role"));
        }

        [Fact]
        public async Task TenantToken_RejectedOnDifferentTenantRoute()
        {
            using var provider = CreateTenantServices(TenantBId);
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var tenantAPrincipal = CreateTenantPrincipal(TenantAId, UserAId, TenantRoles.Owner);

            var userPolicyResult = await authorization.AuthorizeAsync(tenantAPrincipal, TenantPolicies.TenantUser);
            var ownerPolicyResult = await authorization.AuthorizeAsync(tenantAPrincipal, TenantPolicies.TenantOwner);

            Assert.False(userPolicyResult.Succeeded);
            Assert.False(ownerPolicyResult.Succeeded);
        }

        [Fact]
        public async Task PlatformToken_RejectedOnTenantRoute()
        {
            using var provider = CreateTenantServices(TenantAId);
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var platformPrincipal = CreatePlatformPrincipal("SuperAdmin", "PlatformAdmin");

            var userPolicyResult = await authorization.AuthorizeAsync(platformPrincipal, TenantPolicies.TenantUser);
            var ownerPolicyResult = await authorization.AuthorizeAsync(platformPrincipal, TenantPolicies.TenantOwner);

            Assert.False(userPolicyResult.Succeeded);
            Assert.False(ownerPolicyResult.Succeeded);
        }

        [Fact]
        public async Task TenantToken_RejectedOnPlatformRoute()
        {
            using var provider = CreatePlatformServices();
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var tenantOwnerPrincipal = CreateTenantPrincipal(TenantAId, UserAId, TenantRoles.Owner);

            var platformAdminResult = await authorization.AuthorizeAsync(tenantOwnerPrincipal, PlatformAuthorizationPolicies.PlatformAdmin);
            var superAdminResult = await authorization.AuthorizeAsync(tenantOwnerPrincipal, PlatformAuthorizationPolicies.SuperAdmin);

            Assert.False(platformAdminResult.Succeeded);
            Assert.False(superAdminResult.Succeeded);
        }

        [Fact]
        public async Task OwnerOfTenantA_CannotSatisfyTenantOwnerForTenantB()
        {
            using var provider = CreateTenantServices(TenantBId);
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var ownerOfTenantA = CreateTenantPrincipal(TenantAId, UserAId, TenantRoles.Owner);

            var ownerPolicyResult = await authorization.AuthorizeAsync(ownerOfTenantA, TenantPolicies.TenantOwner);

            Assert.False(ownerPolicyResult.Succeeded);
        }

        [Fact]
        public async Task OwnerOfTenantB_SatisfiesTenantOwnerForTenantB()
        {
            using var provider = CreateTenantServices(TenantBId);
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var ownerOfTenantB = CreateTenantPrincipal(TenantBId, UserBId, TenantRoles.Owner);

            var ownerPolicyResult = await authorization.AuthorizeAsync(ownerOfTenantB, TenantPolicies.TenantOwner);
            var userPolicyResult = await authorization.AuthorizeAsync(ownerOfTenantB, TenantPolicies.TenantUser);

            Assert.True(ownerPolicyResult.Succeeded);
            Assert.True(userPolicyResult.Succeeded);
        }

        private sealed class FakeTenantContextAccessor(Guid tenantId) : ITenantContextAccessor
        {
            public TenantContext GetRequired() =>
                TenantContext.Create(tenantId, "secret-ref", true, "slug");
        }
    }
}
