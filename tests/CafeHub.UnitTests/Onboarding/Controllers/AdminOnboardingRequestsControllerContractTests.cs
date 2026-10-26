using System.Linq;
using System.Reflection;
using CafeHub.WebApi.Authorization;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace CafeHub.UnitTests.Onboarding.Controllers
{
    public sealed class AdminOnboardingRequestsControllerContractTests
    {
        [Fact]
        public void List_HasSuperAdminPolicyAttribute()
        {
            var listMethod = typeof(AdminOnboardingRequestsController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .First(m => m.Name == "List");

            var authorize = Assert.Single(listMethod.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)) as AuthorizeAttribute;
            Assert.NotNull(authorize);
            Assert.Equal(PlatformAuthorizationPolicies.SuperAdmin, authorize.Policy);
        }

        [Fact]
        public void Get_HasSuperAdminPolicyAttribute()
        {
            var getMethod = typeof(AdminOnboardingRequestsController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .First(m => m.Name == "Get");

            var authorize = Assert.Single(getMethod.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)) as AuthorizeAttribute;
            Assert.NotNull(authorize);
            Assert.Equal(PlatformAuthorizationPolicies.SuperAdmin, authorize.Policy);
        }
    }
}
