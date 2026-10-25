using Microsoft.AspNetCore.Identity;

namespace CafeHub.Infrastructure.Identity
{
    public static class PlatformAdminPasswordPolicy
    {
        public const int RequiredLength = 12;

        public static void Apply(IdentityOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            options.Password.RequiredLength = RequiredLength;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }
    }
}
