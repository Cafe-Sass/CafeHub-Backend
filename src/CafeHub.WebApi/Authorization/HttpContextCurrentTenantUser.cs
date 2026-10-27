using CafeHub.Application.Identity;
using Microsoft.AspNetCore.Http;

namespace CafeHub.WebApi.Authorization
{
    public sealed class HttpContextCurrentTenantUser : ICurrentTenantUser
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public HttpContextCurrentTenantUser(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

        public Guid? UserId => IsAuthenticated && Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value, out var userId)
            ? userId
            : null;

        public Guid? TenantId => IsAuthenticated && Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst("tid")?.Value, out var tenantId)
            ? tenantId
            : null;

        public IReadOnlyCollection<string> Roles
        {
            get
            {
                if (!IsAuthenticated || httpContextAccessor.HttpContext?.User is not { } user)
                {
                    return Array.Empty<string>();
                }

                return user.Identities
                    .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
                    .Select(claim => claim.Value)
                    .ToArray();
            }
        }
    }
}
