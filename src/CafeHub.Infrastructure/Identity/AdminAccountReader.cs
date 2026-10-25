using CafeHub.Application.Identity;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Identity
{
    internal sealed class AdminAccountReader : IAdminAccountReader
    {
        private readonly UserManager<PlatformAdminUser> userManager;

        public AdminAccountReader(UserManager<PlatformAdminUser> userManager)
        {
            this.userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        public async Task<AuthenticatedAdmin?> FindActiveByIdAsync(Guid adminId, CancellationToken cancellationToken)
        {
            if (adminId == Guid.Empty)
            {
                throw new ArgumentException("The administrator id is required.", nameof(adminId));
            }

            var admin = await userManager.FindByIdAsync(adminId.ToString());

            if (admin is null || !admin.IsActive)
            {
                return null;
            }

            var roles = await userManager.GetRolesAsync(admin);

            return new AuthenticatedAdmin
            {
                Id = admin.Id,
                Email = admin.Email ?? string.Empty,
                Roles = roles.ToList(),
            };
        }
    }
}
