using CafeHub.WebApi.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace CafeHub.WebApi.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class TenantAuthorizeAttribute : AuthorizeAttribute
    {
        public TenantAuthorizeAttribute(string policy)
        {
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            AuthenticationSchemes = TenantAuthenticationSchemes.Bearer;
        }
    }
}
