using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed class PlatformAdminUserStore : UserStore<
        PlatformAdminUser,
        PlatformAdminRole,
        MasterDbContext,
        Guid,
        PlatformAdminUserClaim,
        PlatformAdminUserRole,
        IdentityUserLogin<Guid>,
        IdentityUserToken<Guid>,
        PlatformAdminRoleClaim>
    {
        public PlatformAdminUserStore(MasterDbContext context)
            : base(context)
        {
        }

        protected override Task<PlatformAdminUserRole?> FindUserRoleAsync(
            Guid userId,
            Guid roleId,
            CancellationToken cancellationToken)
        {
            return Context.Set<PlatformAdminUserRole>()
                .FirstOrDefaultAsync(
                    userRole => userRole.UserId == userId && userRole.RoleId == roleId,
                    cancellationToken);
        }
    }
}
