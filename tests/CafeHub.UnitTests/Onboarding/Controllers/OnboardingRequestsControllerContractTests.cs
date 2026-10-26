using System.Linq;
using System.Reflection;
using CafeHub.WebApi.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeHub.UnitTests.Onboarding.Controllers
{
    public sealed class OnboardingRequestsControllerContractTests
    {
        [Fact]
        public void Post_IsAnonymous_AndHasRateLimitAttribute()
        {
            var postMethod = typeof(OnboardingRequestsController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .First(m => m.Name == "Post");

            Assert.True(postMethod.IsDefined(typeof(AllowAnonymousAttribute), inherit: false));
            Assert.True(typeof(OnboardingRequestsController).IsDefined(typeof(EnableRateLimitingAttribute), inherit: false));
        }

        [Fact]
        public void GetStatus_IsAnonymous_AndCarriesRateLimitAttribute()
        {
            var getStatusMethod = typeof(OnboardingRequestsController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .First(m => m.Name == "GetStatus");

            Assert.True(getStatusMethod.IsDefined(typeof(AllowAnonymousAttribute), inherit: false));
            Assert.True(getStatusMethod.IsDefined(typeof(EnableRateLimitingAttribute), inherit: false));
        }
    }
}
