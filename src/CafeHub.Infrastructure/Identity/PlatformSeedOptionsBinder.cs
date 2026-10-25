using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Identity
{
    public static class PlatformSeedOptionsBinder
    {
        public static PlatformSeedOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(PlatformSeedConfigurationKeys.SectionName);

            return new PlatformSeedOptions
            {
                SuperAdminEmail = section?[PlatformSeedConfigurationKeys.SuperAdminEmailKey],
                SuperAdminPassword = section?[PlatformSeedConfigurationKeys.SuperAdminPasswordKey],
            };
        }
    }
}
