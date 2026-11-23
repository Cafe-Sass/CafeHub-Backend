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
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class TenantAuthorizationTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private static ServiceProvider CreateServices(Guid? contextTenantId = null, FakeTenantPermissionReader? permissions = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ITenantPermissionReader>(permissions ?? new FakeTenantPermissionReader { Codes = [TenantPermissions.BranchesRead] });
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
            var ownerOnly = await authorization.AuthorizeAsync(Principal(TenantId), AccessContext(new(false, null, false)), TenantPolicies.TenantOwner);
            var staffUser = await authorization.AuthorizeAsync(Principal(TenantId, "Staff"), TenantPolicies.TenantUser);
            var staffOwner = await authorization.AuthorizeAsync(Principal(TenantId, TenantRoles.Owner), AccessContext(new(true, null, true)), TenantPolicies.TenantOwner);

            Assert.True(ownerUser.Succeeded);
            Assert.True(ownerOnly.Succeeded);
            Assert.True(staffUser.Succeeded);
            Assert.False(staffOwner.Succeeded);
        }

        [Theory]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public async Task BranchReadsRequireAnActiveAssignedBranchOnlyForStaff(bool staff, bool activeBranch, bool allowed)
        {
            using var provider = CreateServices();
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var context = AccessContext(new TenantAccountAccess(staff, null, activeBranch) { BranchId = Guid.NewGuid() });
            var principal = Principal(TenantId, staff ? "Staff" : TenantRoles.Owner);

            Assert.Equal(allowed, (await authorization.AuthorizeAsync(principal, context, TenantPermissions.BranchesRead)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(principal, context, TenantPolicies.TenantUser)).Succeeded);
        }

        [Fact]
        public async Task StaffWithManageAloneCannotReadStaffOrUseOwnerPolicies()
        {
            var permissions = new FakeTenantPermissionReader { Codes = TenantPermissions.All.Except([TenantPermissions.StaffRead]).ToArray() };
            using var provider = CreateServices(permissions: permissions);
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var context = AccessContext(new TenantAccountAccess(true, "staff", true) { BranchId = Guid.NewGuid() });
            var principal = Principal(TenantId, TenantRoles.Owner);

            Assert.True((await authorization.AuthorizeAsync(principal, context, TenantPermissions.StaffManage)).Succeeded);
            Assert.Equal(((TenantAccountAccess)context.Items[typeof(TenantAccountAccess)]!).BranchId, permissions.ExpectedBranchId);
            Assert.False((await authorization.AuthorizeAsync(principal, context, TenantPermissions.StaffRead)).Succeeded);
            foreach (var policy in new[] { TenantPermissions.RolesManage, TenantPermissions.BranchesManage, TenantPermissions.CafeProfileManage, TenantPolicies.TenantOwner })
            {
                Assert.False((await authorization.AuthorizeAsync(principal, context, policy)).Succeeded);
            }
        }

        [Fact]
        public async Task SuspendedStaffKeepTenantUserAccessAndRegainScopedAccessOnReactivation()
        {
            var permissions = new FakeTenantPermissionReader { Codes = [TenantPermissions.StaffManage, TenantPermissions.StaffRead, TenantPermissions.BranchesRead] };
            using var provider = CreateServices(permissions: permissions);
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var account = new TenantAccountAccess(true, "staff", false) { BranchId = Guid.NewGuid() };
            var principal = Principal(TenantId);
            var suspended = AccessContext(account);
            var active = AccessContext(account with { HasActiveBranch = true });

            foreach (var policy in permissions.Codes)
            {
                Assert.False((await authorization.AuthorizeAsync(principal, suspended, policy)).Succeeded);
                Assert.True((await authorization.AuthorizeAsync(principal, active, policy)).Succeeded);
            }

            Assert.True((await authorization.AuthorizeAsync(principal, suspended, TenantPolicies.TenantUser)).Succeeded);
        }

        [Fact]
        public async Task MenuManagePreservesOwnerDelegationBranchGateAndTenantIsolation()
        {
            var grants = new FakeTenantPermissionReader { Codes = [TenantPermissions.MenuManage] };
            using var provider = CreateServices(permissions: grants);
            var authorization = provider.GetRequiredService<IAuthorizationService>();
            var branch = Guid.NewGuid();
            var owner = AccessContext(new TenantAccountAccess(false, null, false));
            var staffAccount = new TenantAccountAccess(true, "staff", true) { BranchId = branch };
            var staff = AccessContext(staffAccount);
            var suspended = AccessContext(staffAccount with { HasActiveBranch = false });

            Assert.True((await authorization.AuthorizeAsync(Principal(TenantId), owner, TenantPermissions.MenuManage)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(Principal(TenantId), staff, TenantPermissions.MenuManage)).Succeeded);
            Assert.Equal(branch, grants.ExpectedBranchId);
            Assert.False((await authorization.AuthorizeAsync(Principal(TenantId), suspended, TenantPermissions.MenuManage)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(Principal(TenantId), suspended, TenantPolicies.TenantUser)).Succeeded);
            Assert.False((await authorization.AuthorizeAsync(Principal(Guid.NewGuid()), staff, TenantPermissions.MenuManage)).Succeeded);

            grants.Codes = [];
            Assert.False((await authorization.AuthorizeAsync(Principal(TenantId), staff, TenantPermissions.MenuManage)).Succeeded);
            Assert.True((await authorization.AuthorizeAsync(Principal(TenantId), staff, TenantPolicies.TenantUser)).Succeeded);
        }

        [Fact]
        public void MenuPermissionDoesNotChangeStationTableOrQrPolicies()
        {
            Assert.Equal(TenantPermissions.StationsManage, typeof(TenantStationsController)
                .GetMethod(nameof(TenantStationsController.Create))!.GetCustomAttribute<TenantAuthorizeAttribute>()!.Policy);
            Assert.Equal(TenantPermissions.TablesManage, typeof(TenantTablesController)
                .GetMethod(nameof(TenantTablesController.Create))!.GetCustomAttribute<TenantAuthorizeAttribute>()!.Policy);
            Assert.Equal(TenantPermissions.TablesManage, typeof(TenantTablesController)
                .GetMethod(nameof(TenantTablesController.RegenerateQr))!.GetCustomAttribute<TenantAuthorizeAttribute>()!.Policy);
            Assert.NotNull(typeof(TenantGuestQrController)
                .GetMethod(nameof(TenantGuestQrController.Resolve))!.GetCustomAttribute<AllowAnonymousAttribute>());
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
        public async Task PlatformAuthorizationDoesNotResolveTheTenantPermissionReader()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ITenantContextAccessor>(new StubTenantContextAccessor(TenantId));
            services.AddScoped<ITenantPermissionReader>(_ => throw new InvalidOperationException("Tenant data is unavailable on platform routes."));
            services.AddPlatformAuthorization();
            services.AddTenantAuthorization();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "SuperAdmin")], "platform");

            Assert.True((await service.AuthorizeAsync(new ClaimsPrincipal(identity), PlatformAuthorizationPolicies.SuperAdmin)).Succeeded);
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
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddControllers().AddApplicationPart(typeof(TenantAuthController).Assembly);
            using var provider = services.BuildServiceProvider();
            var actions = provider.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
                .OfType<ControllerActionDescriptor>()
                .Where(action => action.AttributeRouteInfo?.Template?.StartsWith("t/{slug}/", StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();

            Assert.NotEmpty(actions);
            foreach (var action in actions)
            {
                var tenantAuthorization = action.EndpointMetadata.OfType<TenantAuthorizeAttribute>().LastOrDefault();
                var anonymous = action.EndpointMetadata.OfType<IAllowAnonymous>().Any();
                Assert.True(tenantAuthorization is not null || anonymous, $"{action.DisplayName} has no tenant authorization or AllowAnonymous metadata.");
                if (anonymous)
                {
                    if (action.ControllerTypeInfo.AsType() == typeof(TenantGuestQrController))
                    {
                        Assert.Contains(action.MethodInfo.Name,
                            new[] { nameof(TenantGuestQrController.Resolve), nameof(TenantGuestQrController.Menu), nameof(TenantGuestQrController.PaymentOptions) });
                        Assert.Contains(action.AttributeRouteInfo?.Template, new[]
                        {
                            "t/{slug}/api/v1/guest/qr/{**code}",
                            "t/{slug}/api/v1/guest/qr/{code}/menu",
                            "t/{slug}/api/v1/guest/qr/{code}/payment-options",
                        });
                    }
                    else if (action.ControllerTypeInfo.AsType() == typeof(TenantGuestOrdersController))
                    {
                        var guestOrderTemplates = new Dictionary<string, string>
                        {
                            [nameof(TenantGuestOrdersController.Submit)] = "t/{slug}/api/v1/guest/orders",
                            [nameof(TenantGuestOrdersController.Status)] = "t/{slug}/api/v1/guest/orders/{publicReference}",
                        };
                        Assert.True(guestOrderTemplates.TryGetValue(action.MethodInfo.Name, out var template));
                        Assert.Equal(template, action.AttributeRouteInfo?.Template);
                    }
                    else
                    {
                        Assert.Equal(typeof(TenantAuthController), action.ControllerTypeInfo.AsType());
                        Assert.Contains(action.MethodInfo.Name, new[] { "Login", "Refresh", "Logout" });
                    }
                }
                else
                {
                    Assert.Equal(TenantAuthenticationSchemes.Bearer, tenantAuthorization!.AuthenticationSchemes);
                    Assert.Contains(tenantAuthorization.Policy, TenantPermissions.All.Concat([TenantPolicies.TenantOwner, TenantPolicies.TenantUser]));
                }
            }
        }

        [Fact]
        public void StaffEndpointsRequireTheirExactPermissionsAndKeepCreationAndPasswordResetsOwnerOnly()
        {
            var policies = new Dictionary<string, string>
            {
                [nameof(TenantStaffController.List)] = TenantPermissions.StaffRead,
                [nameof(TenantStaffController.GetById)] = TenantPermissions.StaffRead,
                [nameof(TenantStaffController.Create)] = TenantPolicies.TenantOwner,
                [nameof(TenantStaffController.Update)] = TenantPermissions.StaffManage,
                [nameof(TenantStaffController.Deactivate)] = TenantPermissions.StaffManage,
                [nameof(TenantStaffController.Reactivate)] = TenantPermissions.StaffManage,
                [nameof(TenantStaffController.ResetPassword)] = TenantPolicies.TenantOwner,
                [nameof(TenantStaffController.SetRoles)] = TenantPermissions.RolesManage,
                [nameof(TenantStaffController.SetPermissions)] = TenantPermissions.RolesManage,
                [nameof(TenantStaffController.EffectivePermissions)] = TenantPermissions.RolesManage,
                [nameof(TenantStaffController.SetStaffStations)] = TenantPermissions.StationsManage,
            };

            foreach (var (action, policy) in policies)
            {
                var method = typeof(TenantStaffController).GetMethod(action);
                Assert.Equal(policy, method?.GetCustomAttribute<TenantAuthorizeAttribute>()?.Policy);
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

        private static DefaultHttpContext AccessContext(TenantAccountAccess account)
        {
            var context = new DefaultHttpContext();
            context.Items[typeof(TenantAccountAccess)] = account;
            return context;
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
