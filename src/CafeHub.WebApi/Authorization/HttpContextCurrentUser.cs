using CafeHub.Application.Identity;

namespace CafeHub.WebApi.Authorization
{
    public sealed class HttpContextCurrentUser : ICurrentUser
    {
        private const string SubClaimType = "sub";

        private readonly IHttpContextAccessor httpContextAccessor;

        public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

        public Guid? AdminId
        {
            get
            {
                if (!IsAuthenticated)
                {
                    return null;
                }

                var subClaimValue = httpContextAccessor.HttpContext?.User.FindFirst(SubClaimType)?.Value;
                return Guid.TryParse(subClaimValue, out var adminId) ? adminId : null;
            }
        }

        public IReadOnlyCollection<string> Roles
        {
            get
            {
                if (!IsAuthenticated)
                {
                    return Array.Empty<string>();
                }

                var user = httpContextAccessor.HttpContext?.User;
                if (user is null)
                {
                    return Array.Empty<string>();
                }

                var roles = new List<string>();
                foreach (var identity in user.Identities)
                {
                    roles.AddRange(identity.FindAll(identity.RoleClaimType).Select(claim => claim.Value));
                }

                return roles;
            }
        }
    }
}
