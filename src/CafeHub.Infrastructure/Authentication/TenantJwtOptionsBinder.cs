using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Authentication
{
    public static class TenantJwtOptionsBinder
    {
        public static TenantJwtOptions Bind(IConfiguration configuration, string platformAudience)
        {
            var section = configuration.GetSection("Jwt:Tenant");
            var options = new TenantJwtOptions();
            if (int.TryParse(section["AccessTokenLifetimeMinutes"], out var lifetime)) options.AccessTokenLifetimeMinutes = lifetime;
            options.Audience = section["Audience"] ?? string.Empty;
            var errors = options.Validate(platformAudience);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
            return options;
        }
    }
}
