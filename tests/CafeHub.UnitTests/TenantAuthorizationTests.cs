using System.Reflection;
using System.Security.Claims;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.WebApi;
using CafeHub.WebApi.Authentication;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class TenantAuthorizationTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private static ServiceProvider CreateServices(Guid? contextTenantId = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ITenantContextAccessor>(new StubTenantContextAccessor(contextTenantId ?? TenantId));
            services.AddTenantAuthentication();
            services.AddTenantAuthorization();
            return services.BuildServiceProvider();
        }

        private static ClaimsPrincipal Principal(Guid tenantId, params string[] roles)
        {
            var claims = new List<Claim>
            {
                new("tid", tenantId.ToString()),
                new("sub", UserId.ToString()),
            };
            claims.AddRange(roles.Select(role => new Claim("role", role)));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TenantTest", "sub", "role"));
        }

        [Fact]
        public async Task OwnerAndStaff_SatisfyOnlyExpectedTenantPolicies()
        {
            using var provider = CreateServices();
            var authorization = provider.GetRequiredService<IAuthorizationService>();

            var ownerUser = await authorization.AuthorizeAsync(Principal(TenantId, TenantRoles.Owner), TenantPolicies.TenantUser);
            var ownerOnly = await authorization.AuthorizeAsync(Principal(TenantId, TenantRoles.Owner), TenantPolicies.TenantOwner);
            var staffUser = await authorization.AuthorizeAsync(Principal(TenantId, "Staff"), TenantPolicies.TenantUser);
            var staffOwner = await authorization.AuthorizeAsync(Principal(TenantId, "Staff"), TenantPolicies.TenantOwner);

            Assert.True(ownerUser.Succeeded);
            Assert.True(ownerOnly.Succeeded);
            Assert.True(staffUser.Succeeded);
            Assert.False(staffOwner.Succeeded);
        }

        [Fact]
        public async Task PlatformAnonymousAndCrossTenantPrincipals_SatisfyNeitherPolicy()
        {
            using var provider = CreateServices();
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var principals = new[]
            {
                new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("role", "SuperAdmin") }, "PlatformTest", "sub", "role")),
                new ClaimsPrincipal(new ClaimsIdentity()),
                Principal(Guid.NewGuid(), TenantRoles.Owner),
            };

            foreach (var principal in principals)
            {
                Assert.False((await authorization.AuthorizeAsync(principal, TenantPolicies.TenantUser)).Succeeded);
                Assert.False((await authorization.AuthorizeAsync(principal, TenantPolicies.TenantOwner)).Succeeded);
            }
        }

        [Fact]
        public void TenantAuthorizeAttribute_SelectsTenantSchemeAndPolicy()
        {
            var attribute = new TenantAuthorizeAttribute(TenantPolicies.TenantUser);

            Assert.Equal(TenantAuthenticationSchemes.Bearer, attribute.AuthenticationSchemes);
            Assert.Equal(TenantPolicies.TenantUser, attribute.Policy);
        }

        [Fact]
        public void TenantControllerActions_AreExplicitlyAuthorizedOrAnonymous()
        {
            var controllerTypes = typeof(TenantAuthController).Assembly.GetTypes()
                .Where(type => typeof(ControllerBase).IsAssignableFrom(type)
                    && type.GetCustomAttribute<RouteAttribute>()?.Template.StartsWith("t/{slug}", StringComparison.Ordinal) == true);
            var actions = controllerTypes.SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes().Any(attribute => attribute is HttpGetAttribute or HttpPostAttribute or HttpPutAttribute or HttpDeleteAttribute or HttpPatchAttribute))
                .Select(method => (Type: type, Method: method)))
                .ToArray();

            Assert.NotEmpty(actions);
            foreach (var (type, method) in actions)
            {
                var tenantAuthorization = method.GetCustomAttribute<TenantAuthorizeAttribute>()
                    ?? type.GetCustomAttribute<TenantAuthorizeAttribute>();
                var anonymous = method.GetCustomAttribute<AllowAnonymousAttribute>()
                    ?? type.GetCustomAttribute<AllowAnonymousAttribute>();
                Assert.True(tenantAuthorization is not null || anonymous is not null, $"{type.Name}.{method.Name} has no tenant authorization or AllowAnonymous attribute.");
            }
        }

        [Fact]
        public void CurrentTenantUser_MapsAuthenticatedGuidClaimsAndRoles()
        {
            var context = new DefaultHttpContext { User = Principal(TenantId, TenantRoles.Owner, "Staff") };
            var currentUser = new HttpContextCurrentTenantUser(new StubHttpContextAccessor(context));

            Assert.True(currentUser.IsAuthenticated);
            Assert.Equal(UserId, currentUser.UserId);
            Assert.Equal(TenantId, currentUser.TenantId);
            Assert.Equal(new[] { TenantRoles.Owner, "Staff" }, currentUser.Roles);
        }

        [Fact]
        public void CurrentTenantUser_ReturnsEmptyValuesForAnonymousOrInvalidIds()
        {
            var anonymous = new HttpContextCurrentTenantUser(new StubHttpContextAccessor(null));
            var invalidContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim("sub", "invalid"),
                    new Claim("tid", "invalid"),
                    new Claim("role", "Owner"),
                }, "TenantTest", "sub", "role")),
            };
            var invalid = new HttpContextCurrentTenantUser(new StubHttpContextAccessor(invalidContext));

            Assert.False(anonymous.IsAuthenticated);
            Assert.Null(anonymous.UserId);
            Assert.Null(anonymous.TenantId);
            Assert.Empty(anonymous.Roles);
            Assert.True(invalid.IsAuthenticated);
            Assert.Null(invalid.UserId);
            Assert.Null(invalid.TenantId);
            Assert.Equal(new[] { "Owner" }, invalid.Roles);
        }

        private sealed class StubTenantContextAccessor(Guid tenantId) : ITenantContextAccessor
        {
            public TenantContext GetRequired() => TenantContext.Create(tenantId, "tenant-secret-ref", true);
        }

        private sealed class StubHttpContextAccessor(HttpContext? context) : IHttpContextAccessor
        {
            public HttpContext? HttpContext { get; set; } = context;
        }
    }
}
